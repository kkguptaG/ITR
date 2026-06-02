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
/// Approving a Form 16 extraction prefills the return with a full SalaryDetail (the entity the engine,
/// ITR generator and Schedule S read — not a thin IncomeSource), a deductor-wise salary TdsEntry that
/// rolls into the TDS credit, and the 80C/80D deductions. Real Sqlite AppDbContext + real DocumentService.
/// </summary>
public sealed class Form16PrefillTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TenantId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    // FieldsJson is the portable {key:{value,confidence,source}} map DocumentService persists (camelCase).
    private const string Form16FieldsJson = """
    {
      "_docClass": {"value":"form16","confidence":0.95,"source":"rule"},
      "form16.part_a.employer_name": {"value":"Acme Corp","confidence":0.98,"source":"ocr"},
      "form16.part_a.employer_tan": {"value":"DELH12345A","confidence":0.98,"source":"ocr"},
      "form16.part_b.gross_salary_17_1": {"value":"900000","confidence":0.95,"source":"ocr"},
      "form16.part_b.hra_exempt_10_13a": {"value":"120000","confidence":0.95,"source":"ocr"},
      "form16.part_b.std_deduction_16ia": {"value":"50000","confidence":0.99,"source":"ocr"},
      "form16.part_b.professional_tax_16iii": {"value":"2400","confidence":0.99,"source":"ocr"},
      "form16.part_b.deduction_80c": {"value":"100000","confidence":0.95,"source":"ocr"},
      "form16.part_b.deduction_80d": {"value":"20000","confidence":0.95,"source":"ocr"},
      "form16.part_b.tds_total": {"value":"60000","confidence":0.95,"source":"ocr"}
    }
    """;

    [Fact]
    public async Task Approving_a_form16_prefills_salary_breakup_tds_and_deductions()
    {
        await using var db = NewContext();
        var ret = NewReturn();
        var doc = new Document
        {
            Id = Guid.NewGuid(), TenantId = TenantId, UserId = UserId, TaxReturnId = ret.Id,
            Kind = DocumentKind.Form16, FileName = "form16.pdf", ContentType = "application/pdf",
            Status = DocumentStatus.NeedsReview, StoragePath = "k/form16.pdf",
        };
        db.TaxReturns.Add(ret);
        db.Documents.Add(doc);
        db.DocumentExtractions.Add(new DocumentExtraction
        {
            Id = Guid.NewGuid(), TenantId = TenantId, DocumentId = doc.Id,
            Status = DocumentStatus.NeedsReview, ConfidenceScore = 0.95m, FieldsJson = Form16FieldsJson,
        });
        await db.SaveChangesAsync();

        var svc = new DocumentService(db, new NoopStorage(), new ExtractionService(),
            new FakeCurrentUser(), new FakeClock(), NullLogger<DocumentService>.Instance);

        var resp = await svc.ApproveExtractionAsync(doc.Id, new ApproveExtractionRequest(MapToReturn: true));

        // A full SalaryDetail (consumed by the engine + generator + Schedule S), matched by Form16DocumentId.
        var sal = await db.SalaryDetails.SingleAsync(s => s.TaxReturnId == ret.Id);
        sal.Gross.Should().Be(900_000m);
        sal.HraExemption.Should().Be(120_000m);
        sal.StdDeduction.Should().Be(50_000m);
        sal.ProfessionalTax.Should().Be(2_400m);
        sal.Employer.Should().Be("Acme Corp");
        sal.Tan.Should().Be("DELH12345A");
        sal.Form16DocumentId.Should().Be(doc.Id);

        // The thin IncomeSource(Salary) path is NOT used for Form 16 (it would be ignored downstream).
        (await db.IncomeSources.CountAsync(s => s.TaxReturnId == ret.Id && s.Type == IncomeType.Salary))
            .Should().Be(0);

        // Salary TDS → a deductor-wise entry + the return's TDS credit.
        var tds = await db.TdsEntries.SingleAsync(t => t.TaxReturnId == ret.Id && t.Head == TdsHead.Salary);
        tds.TaxDeducted.Should().Be(60_000m);
        tds.IncomeOffered.Should().Be(900_000m);
        tds.DeductorTan.Should().Be("DELH12345A");
        (await db.TaxReturns.FirstAsync(r => r.Id == ret.Id)).TdsPaid.Should().Be(60_000m);

        // 80C / 80D deductions.
        resp.DeductionsUpserted.Should().Be(2);
        (await db.Deductions.FirstAsync(d => d.TaxReturnId == ret.Id && d.Section == "80C")).Amount.Should().Be(100_000m);
        (await db.Deductions.FirstAsync(d => d.TaxReturnId == ret.Id && d.Section == "80D")).Amount.Should().Be(20_000m);
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
        public Guid UserId => Form16PrefillTests.UserId;
        public Guid TenantId => Form16PrefillTests.TenantId;
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

    // ApproveExtraction never touches storage — a throwing stub guarantees that stays true.
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
