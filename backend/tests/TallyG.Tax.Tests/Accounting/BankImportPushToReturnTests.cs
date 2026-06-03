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
using TallyG.Tax.Api.Modules.Accounting;
using TallyG.Tax.Domain.Abstractions;
using TallyG.Tax.Domain.Entities;
using TallyG.Tax.Domain.Enums;
using TallyG.Tax.Infrastructure.Persistence;
using Xunit;

namespace TallyG.Tax.Tests.Accounting;

/// <summary>
/// BankImportService.PushToReturnAsync: posted OtherIncome credit lines become nature-tagged
/// IncomeSource rows on the return. Real Sqlite + real service.
/// </summary>
public sealed class BankImportPushToReturnTests
{
    private static readonly Guid UserId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private static readonly Guid TenantId = Guid.Parse("88888888-8888-8888-8888-888888888888");

    [Fact]
    public async Task Posting_pushes_other_income_credit_lines_to_return_as_nature_tagged_sources()
    {
        await using var db = NewContext();
        var ret = NewReturn();
        var bankLedger = new Ledger { TenantId = TenantId, UserId = UserId, Name = "SBI Savings Account", Group = LedgerGroup.BankAccounts };
        var interestLedger = new Ledger { TenantId = TenantId, UserId = UserId, Name = "SBI Savings Interest (E)", Group = LedgerGroup.OtherIncome };

        var import = new BankStatementImport
        {
            TenantId = TenantId, UserId = UserId,
            FileName = "statement.csv", Status = BankImportStatus.Posted,
            BankLedgerId = bankLedger.Id,
        };

        // A posted credit line for savings interest.
        var voucher = new Voucher { TenantId = TenantId };
        var line = new BankStatementLine
        {
            TenantId = TenantId, ImportId = import.Id,
            Direction = DrCr.Credit, Amount = 12_000m,
            Status = BankLineStatus.Posted,
            ChosenLedgerId = interestLedger.Id,
            VoucherId = voucher.Id,
        };

        db.TaxReturns.Add(ret);
        db.Ledgers.Add(bankLedger);
        db.Ledgers.Add(interestLedger);
        db.BankStatementImports.Add(import);
        db.Vouchers.Add(voucher);
        db.BankStatementLines.Add(line);
        await db.SaveChangesAsync();

        var svc = new BankImportService(db, new NoopStorage(), new NoopParser(), new NoopMatcher(),
            new FakeCurrentUser(), new FakeClock(), NullLogger<BankImportService>.Instance);

        var count = await svc.PushToReturnAsync(import.Id, ret.Id);

        count.Should().Be(1);
        var sources = await db.IncomeSources.Where(s => s.TaxReturnId == ret.Id).ToListAsync();
        sources.Should().HaveCount(1);
        var src = sources[0];
        src.Amount.Should().Be(12_000m);
        TaxComputationInputFactory.ExtractNature(src.SourceMetaJson).Should().Be("savings_interest");
    }

    [Fact]
    public async Task Push_is_idempotent_and_updates_amount_on_second_call()
    {
        await using var db = NewContext();
        var ret = NewReturn();
        var bankLedger = new Ledger { TenantId = TenantId, UserId = UserId, Name = "HDFC Bank", Group = LedgerGroup.BankAccounts };
        var divLedger = new Ledger { TenantId = TenantId, UserId = UserId, Name = "Equity Dividend (E)", Group = LedgerGroup.OtherIncome };
        var import = new BankStatementImport { TenantId = TenantId, UserId = UserId, FileName = "stmt.csv", Status = BankImportStatus.Posted, BankLedgerId = bankLedger.Id };
        var voucher = new Voucher { TenantId = TenantId };
        var line = new BankStatementLine { TenantId = TenantId, ImportId = import.Id, Direction = DrCr.Credit, Amount = 8_000m, Status = BankLineStatus.Posted, ChosenLedgerId = divLedger.Id, VoucherId = voucher.Id };

        db.TaxReturns.Add(ret); db.Ledgers.AddRange(bankLedger, divLedger);
        db.BankStatementImports.Add(import); db.Vouchers.Add(voucher); db.BankStatementLines.Add(line);
        await db.SaveChangesAsync();

        var svc = new BankImportService(db, new NoopStorage(), new NoopParser(), new NoopMatcher(),
            new FakeCurrentUser(), new FakeClock(), NullLogger<BankImportService>.Instance);

        await svc.PushToReturnAsync(import.Id, ret.Id);
        line.Amount = 9_500m;  // simulate a re-import with updated amount
        await db.SaveChangesAsync();
        var count2 = await svc.PushToReturnAsync(import.Id, ret.Id);

        count2.Should().Be(1);
        var src = await db.IncomeSources.SingleAsync(s => s.TaxReturnId == ret.Id);
        src.Amount.Should().Be(9_500m, "second push should update, not duplicate");
        TaxComputationInputFactory.ExtractNature(src.SourceMetaJson).Should().Be("dividend");
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
        public Guid UserId => BankImportPushToReturnTests.UserId;
        public Guid TenantId => BankImportPushToReturnTests.TenantId;
        public IReadOnlyList<string> Roles => new[] { "User" };
        public bool IsAuthenticated => true;
        public Guid? SessionId => null;
        public bool IsInRole(string role) => false;
    }

    private sealed class FakeClock : IDateTime
    {
        public DateTimeOffset UtcNow => new(2026, 6, 3, 0, 0, 0, TimeSpan.Zero);
        public DateOnly TodayIst => new(2026, 6, 3);
    }

    private sealed class NoopStorage : IFileStorage
    {
        public Task<PresignedUpload> CreateUploadUrlAsync(string k, string c, TimeSpan v, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<string> CreateDownloadUrlAsync(string k, TimeSpan v, CancellationToken ct = default) => throw new NotSupportedException();
        public Task SaveAsync(string k, byte[] c, string ct2, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<byte[]?> ReadAsync(string k, CancellationToken ct = default) => Task.FromResult<byte[]?>(null);
        public Task<bool> ExistsAsync(string k, CancellationToken ct = default) => Task.FromResult(false);
        public Task DeleteAsync(string k, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class NoopParser : IBankStatementParser
    {
        public BankStatementParseResult Parse(byte[] content, string contentType, string fileName)
            => new(Array.Empty<ParsedBankLine>(), Array.Empty<string>(), null, null);
    }

    private sealed class NoopMatcher : ILedgerMatchingService
    {
        public LedgerSuggestion Suggest(string narration, DrCr direction, IReadOnlyCollection<Ledger> existing)
            => new(null, "Suspense (E)", LedgerGroup.Suspense, true, 0m, "noop", "test stub");
    }
}
