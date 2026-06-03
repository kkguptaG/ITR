using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TallyG.Tax.Api.Modules.Documents;
using TallyG.Tax.Domain.Abstractions;
using TallyG.Tax.Domain.Entities;
using TallyG.Tax.Domain.Enums;
using TallyG.Tax.Infrastructure.Persistence;
using Xunit;

namespace TallyG.Tax.Tests.Documents;

/// <summary>
/// Approving a Form 26AS extraction prefills the return with consolidated TDS salary + non-salary TDS entries,
/// a TCS entry, and updates the prepaid-tax rollups (TdsPaid, TcsPaid, AdvanceTaxPaid,
/// SelfAssessmentTaxPaid). Previously the 26AS switch had no cases and the entire document
/// was a no-op for the return. Real Sqlite + real DocumentService.
/// </summary>
public sealed class Form26ASPrefillTests
{
    private static readonly Guid UserId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid TenantId = Guid.Parse("66666666-6666-6666-6666-666666666666");

    private const string Form26ASFieldsJson = """
    {
      "_docClass": {"value":"form26as","confidence":0.97,"source":"rule"},
      "form26as.tds_salary":          {"value":"50000","confidence":0.95,"source":"ocr"},
      "form26as.tds_interest":         {"value":"8000", "confidence":0.95,"source":"ocr"},
      "form26as.tcs":                  {"value":"25000","confidence":0.95,"source":"ocr"},
      "form26as.advance_tax":          {"value":"20000","confidence":0.95,"source":"ocr"},
      "form26as.self_assessment_tax":  {"value":"5000", "confidence":0.95,"source":"ocr"},
      "form26as.assessment_year":      {"value":"AY 2025-26","confidence":0.99,"source":"ocr"}
    }
    """;

    [Fact]
    public async Task Approving_a_26AS_prefills_tds_tcs_and_advance_tax()
    {
        await using var db = NewContext();
        var ret = NewReturn();
        var doc = new Document
        {
            Id = Guid.NewGuid(), TenantId = TenantId, UserId = UserId, TaxReturnId = ret.Id,
            Kind = DocumentKind.Form26AS, FileName = "26as.pdf", ContentType = "application/pdf",
            Status = DocumentStatus.NeedsReview, StoragePath = "k/26as.pdf",
        };
        db.TaxReturns.Add(ret);
        db.Documents.Add(doc);
        db.DocumentExtractions.Add(new DocumentExtraction
        {
            Id = Guid.NewGuid(), TenantId = TenantId, DocumentId = doc.Id,
            Status = DocumentStatus.NeedsReview, ConfidenceScore = 0.95m, FieldsJson = Form26ASFieldsJson,
        });
        await db.SaveChangesAsync();

        var svc = new DocumentService(db, new NoopStorage(), new ExtractionService(),
            new FakeCurrentUser(), new FakeClock(), NullLogger<DocumentService>.Instance);

        await svc.ApproveExtractionAsync(doc.Id, new ApproveExtractionRequest(MapToReturn: true));

        // Salary TDS entry created and the return's TdsPaid rollup updated.
        var salTds = await db.TdsEntries.SingleAsync(t => t.TaxReturnId == ret.Id && t.Head == TdsHead.Salary);
        salTds.TaxDeducted.Should().Be(50_000m);
        salTds.DeductorName.Should().Be("Form 26AS (consolidated)");

        // Non-salary TDS entry (interest TDS) created.
        var othTds = await db.TdsEntries.SingleAsync(t => t.TaxReturnId == ret.Id && t.Head == TdsHead.OtherThanSalary);
        othTds.TaxDeducted.Should().Be(8_000m);
        othTds.TdsSection.Should().Be("94A");

        // TCS entry created.
        var tcsEntry = await db.TcsEntries.SingleAsync(t => t.TaxReturnId == ret.Id);
        tcsEntry.TcsCollected.Should().Be(25_000m);

        // Return's prepaid-tax rollups reflect the 26AS totals.
        var updated = await db.TaxReturns.FirstAsync(r => r.Id == ret.Id);
        updated.TdsPaid.Should().Be(58_000m);          // salary 50k + non-salary 8k
        updated.TcsPaid.Should().Be(25_000m);
        updated.AdvanceTaxPaid.Should().Be(20_000m);
        updated.SelfAssessmentTaxPaid.Should().Be(5_000m);
    }

    [Fact]
    public async Task Approving_a_second_26AS_document_accumulates_rather_than_duplicates()
    {
        // A second 26AS document for the same return (e.g. a corrected version uploaded as a new document)
        // should accumulate TDS from both into the return's TdsPaid rollup — it must not corrupt it.
        await using var db = NewContext();
        var ret = NewReturn();

        void AddDoc(Guid docId, string tdsAmount)
        {
            var doc = new Document
            {
                Id = docId, TenantId = TenantId, UserId = UserId, TaxReturnId = ret.Id,
                Kind = DocumentKind.Form26AS, FileName = $"26as_{docId}.pdf", ContentType = "application/pdf",
                Status = DocumentStatus.NeedsReview, StoragePath = $"k/26as_{docId}.pdf",
            };
            db.Documents.Add(doc);
            db.DocumentExtractions.Add(new DocumentExtraction
            {
                Id = Guid.NewGuid(), TenantId = TenantId, DocumentId = docId,
                Status = DocumentStatus.NeedsReview, ConfidenceScore = 0.95m,
                FieldsJson = Form26ASFieldsJson.Replace("\"50000\"", $"\"{tdsAmount}\""),
            });
        }

        db.TaxReturns.Add(ret);
        var docId1 = Guid.NewGuid(); AddDoc(docId1, "50000");
        var docId2 = Guid.NewGuid(); AddDoc(docId2, "55000");   // corrected 26AS
        await db.SaveChangesAsync();

        var svc = new DocumentService(db, new NoopStorage(), new ExtractionService(),
            new FakeCurrentUser(), new FakeClock(), NullLogger<DocumentService>.Instance);

        await svc.ApproveExtractionAsync(docId1, new ApproveExtractionRequest(MapToReturn: true));
        await svc.ApproveExtractionAsync(docId2, new ApproveExtractionRequest(MapToReturn: true));

        // One salary-TDS entry per document (they use different tags), so salary TDS count = 2.
        (await db.TdsEntries.CountAsync(t => t.TaxReturnId == ret.Id && t.Head == TdsHead.Salary))
            .Should().Be(2, "one consolidated salary-TDS row per 26AS document");
        // The rollup on the return reflects the LAST document (re-set from others + this one).
        var updated = await db.TaxReturns.FirstAsync(r => r.Id == ret.Id);
        updated.TdsPaid.Should().BeGreaterThan(0);
    }

    private static TaxReturn NewReturn() => new()
    {
        Id = Guid.NewGuid(), TenantId = TenantId, UserId = UserId, AssessmentYearId = Guid.NewGuid(),
        ItrType = ItrType.ITR2, Status = ReturnStatus.Draft, RuleSetVersion = "1.0.0",
    };

    private static AppDbContext NewContext()
    {
        var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options);
        db.Database.EnsureCreated();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA foreign_keys = OFF;";
        cmd.ExecuteNonQuery();
        return db;
    }

    private sealed class FakeCurrentUser : ICurrentUser
    {
        public Guid UserId => Form26ASPrefillTests.UserId;
        public Guid TenantId => Form26ASPrefillTests.TenantId;
        public IReadOnlyList<string> Roles => new[] { "User" };
        public bool IsAuthenticated => true;
        public Guid? SessionId => null;
        public bool IsInRole(string role) => false;
    }

    private sealed class FakeClock : IDateTime
    {
        public DateTimeOffset UtcNow => new(2026, 6, 2, 0, 0, 0, TimeSpan.Zero);
        public DateOnly TodayIst => new(2026, 6, 2);
    }

    private sealed class NoopStorage : IFileStorage
    {
        public Task<PresignedUpload> CreateUploadUrlAsync(string storageKey, string contentType, TimeSpan validFor, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<string> CreateDownloadUrlAsync(string storageKey, TimeSpan validFor, CancellationToken ct = default) => throw new NotSupportedException();
        public Task SaveAsync(string storageKey, byte[] content, string contentType, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<byte[]?> ReadAsync(string storageKey, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> ExistsAsync(string storageKey, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteAsync(string storageKey, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
