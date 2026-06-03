using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TallyG.Tax.Api.Common;
using TallyG.Tax.Api.Modules.Documents;
using TallyG.Tax.Domain.Abstractions;
using TallyG.Tax.Domain.Entities;
using TallyG.Tax.Domain.Enums;
using TallyG.Tax.Infrastructure.Persistence;
using Xunit;

namespace TallyG.Tax.Tests.Documents;

/// <summary>
/// Approving an AIS extraction prefills the return with ONE nature-tagged other-sources row per head
/// (savings / term-deposit / other / refund interest, dividend) — not a single untagged lump — so
/// Schedule OS itemises them and the AIS/26AS reconciliation matches head-by-head. Real Sqlite + service.
/// </summary>
public sealed class AisPrefillTests
{
    private static readonly Guid UserId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid TenantId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private const string AisFieldsJson = """
    {
      "_docClass": {"value":"ais","confidence":0.96,"source":"rule"},
      "ais.interest_savings_bank": {"value":"12000","confidence":0.95,"source":"ocr"},
      "ais.interest_term_deposit": {"value":"30000","confidence":0.95,"source":"ocr"},
      "ais.interest_others": {"value":"5000","confidence":0.95,"source":"ocr"},
      "ais.interest_income_tax_refund": {"value":"1500","confidence":0.95,"source":"ocr"},
      "ais.dividend_income": {"value":"8000","confidence":0.95,"source":"ocr"}
    }
    """;

    [Fact]
    public async Task Approving_an_AIS_prefills_one_nature_tagged_row_per_head()
    {
        await using var db = NewContext();
        var ret = NewReturn();
        var doc = new Document
        {
            Id = Guid.NewGuid(), TenantId = TenantId, UserId = UserId, TaxReturnId = ret.Id,
            Kind = DocumentKind.AIS, FileName = "ais.json", ContentType = "application/json",
            Status = DocumentStatus.NeedsReview, StoragePath = "k/ais.json",
        };
        db.TaxReturns.Add(ret);
        db.Documents.Add(doc);
        db.DocumentExtractions.Add(new DocumentExtraction
        {
            Id = Guid.NewGuid(), TenantId = TenantId, DocumentId = doc.Id,
            Status = DocumentStatus.NeedsReview, ConfidenceScore = 0.95m, FieldsJson = AisFieldsJson,
        });
        await db.SaveChangesAsync();

        var svc = new DocumentService(db, new NoopStorage(), new ExtractionService(),
            new FakeCurrentUser(), new FakeClock(), NullLogger<DocumentService>.Instance);

        await svc.ApproveExtractionAsync(doc.Id, new ApproveExtractionRequest(MapToReturn: true));

        var rows = await db.IncomeSources
            .Where(s => s.TaxReturnId == ret.Id && s.Type == IncomeType.OtherSources).ToListAsync();
        rows.Should().HaveCount(5, "each AIS head maps to its own nature-tagged row, not one lump");

        decimal AmountFor(string nature) =>
            rows.Single(r => TaxComputationInputFactory.ExtractNature(r.SourceMetaJson) == nature).Amount;

        AmountFor("savings_interest").Should().Be(12_000m);
        AmountFor("fd_interest").Should().Be(30_000m);
        AmountFor("interest").Should().Be(5_000m);
        AmountFor("refund_interest").Should().Be(1_500m);
        AmountFor("dividend").Should().Be(8_000m);
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
        public Guid UserId => AisPrefillTests.UserId;
        public Guid TenantId => AisPrefillTests.TenantId;
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
