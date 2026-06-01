using System.Text;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TallyG.Tax.Api.Modules.Accounting;
using TallyG.Tax.Domain.Abstractions;
using TallyG.Tax.Domain.Enums;
using TallyG.Tax.Infrastructure.Persistence;
using TallyG.Tax.Infrastructure.Services;
using Xunit;

namespace TallyG.Tax.Tests.Accounting;

/// <summary>
/// End-to-end behavioural test of the bank-statement → double-entry mechanism using the REAL parser
/// (<see cref="BankStatementParser"/>), the REAL matcher (<see cref="LedgerMatchingService"/>) and a
/// real Sqlite <see cref="AppDbContext"/>. Proves: lines are parsed, the matcher classifies by
/// keyword and proposes " (E)" heads for the rest, repeated proposals collapse to one ledger, and
/// posting writes balanced vouchers that move the bank balance correctly.
/// </summary>
public sealed class BankImportFlowTests
{
    [Fact]
    public async Task Upload_then_post_creates_E_ledgers_and_balanced_double_entry_vouchers()
    {
        await using var ctx = NewContext(out var db);

        var service = new BankImportService(
            db,
            new InMemoryFileStorage(),
            new BankStatementParser(),
            new LedgerMatchingService(),
            new FakeCurrentUser(),
            new FakeClock(),
            NullLogger<BankImportService>.Instance);

        // A statement with separate Withdrawal/Deposit columns covering: a salary credit (income),
        // two Swiggy debits (one keyword category, deduped), a rent debit, an electricity debit, and
        // an unknown counterparty debit (→ a counterparty-named " (E)" head).
        const string csv = """
            Date,Narration,Reference,Withdrawal,Deposit,Balance
            01/04/2025,SALARY CREDIT INFOSYS LTD,REF1,,85000.00,185000.00
            03/04/2025,UPI/DR/405123/SWIGGY/HDFC,REF2,640.00,,184360.00
            05/04/2025,NEFT RENT PAID TO LANDLORD,REF3,18000.00,,166360.00
            07/04/2025,BSES ELECTRICITY BILL APR,REF4,2200.00,,164160.00
            10/04/2025,UPI/DR/990011/SWIGGY/HDFC,REF5,720.00,,163440.00
            12/04/2025,UPI/DR/12345/ACME TRADERS/ICICI,REF6,5000.00,,158440.00
            """;

        var detail = await service.UploadAsync(
            new MemoryStream(Encoding.UTF8.GetBytes(csv)), "statement.csv", "text/csv", null, default);

        // --- parsing + matching ---
        detail.Lines.Should().HaveCount(6);
        detail.Import.Status.Should().Be(BankImportStatus.NeedsReview);

        var salary = detail.Lines.Single(l => l.Narration.Contains("SALARY"));
        salary.Direction.Should().Be(DrCr.Credit);
        salary.Credit.Should().Be(85000.00m);
        salary.SuggestedGroup.Should().Be(LedgerGroup.OtherIncome); // money-in salary → income

        var rent = detail.Lines.Single(l => l.Narration.Contains("RENT"));
        rent.Direction.Should().Be(DrCr.Debit);
        rent.SuggestedLedgerName.Should().Contain("Rent");

        var electricity = detail.Lines.Single(l => l.Narration.Contains("ELECTRICITY"));
        electricity.SuggestedLedgerName.Should().Contain("Electricity");

        // Both Swiggy lines resolve to the SAME suggested category head.
        var swiggy = detail.Lines.Where(l => l.Narration.Contains("SWIGGY")).ToList();
        swiggy.Should().HaveCount(2);
        swiggy.Select(l => l.SuggestedLedgerName).Distinct().Should().ContainSingle();

        // The unknown counterparty becomes a derived " (E)" head.
        var acme = detail.Lines.Single(l => l.Narration.Contains("ACME"));
        acme.SuggestionIsNewLedger.Should().BeTrue();
        acme.MatchMethod.Should().Be("counterparty");
        acme.SuggestedLedgerName.Should().Be("Acme Traders (E)");

        // --- post (accept all suggestions) ---
        var post = await service.PostAsync(detail.Import.Id, new PostImportRequest(), default);

        post.VouchersPosted.Should().Be(6);
        post.Import.Status.Should().Be(BankImportStatus.Posted);
        post.Import.PostedCount.Should().Be(6);

        // Every voucher is a balanced two-legged double entry.
        var vouchers = await db.Vouchers.Include(v => v.Entries).ToListAsync();
        vouchers.Should().HaveCount(6);
        foreach (var v in vouchers)
        {
            v.Entries.Should().HaveCount(2);
            var dr = v.Entries.Where(e => e.Direction == DrCr.Debit).Sum(e => e.Amount);
            var cr = v.Entries.Where(e => e.Direction == DrCr.Credit).Sum(e => e.Amount);
            dr.Should().Be(cr, "a voucher must balance");
            dr.Should().Be(v.Amount);
        }

        // Swiggy collapsed to exactly one generated head used by both of its lines.
        var ledgers = await db.Ledgers.ToListAsync();
        var generated = ledgers.Where(l => l.IsSystemGenerated && l.Name.EndsWith("(E)")).ToList();
        generated.Should().Contain(l => l.Name == "Acme Traders (E)");
        ledgers.Count(l => l.Name.Contains("Staff Welfare")).Should().Be(1);

        // The bank ledger nets debits (deposits) minus credits (withdrawals):
        // +85000 − (640 + 18000 + 2200 + 720 + 5000) = 58440.
        var bank = ledgers.Single(l => l.IsBank);
        var bankEntries = await db.VoucherEntries.Where(e => e.LedgerId == bank.Id).ToListAsync();
        var bankNet = bankEntries.Where(e => e.Direction == DrCr.Debit).Sum(e => e.Amount)
                      - bankEntries.Where(e => e.Direction == DrCr.Credit).Sum(e => e.Amount);
        bankNet.Should().Be(58440.00m);

        // Re-posting is idempotent: nothing new is written.
        var repost = await service.PostAsync(detail.Import.Id, new PostImportRequest(), default);
        repost.VouchersPosted.Should().Be(0);
        (await db.Vouchers.CountAsync()).Should().Be(6);
    }

    [Fact]
    public void Parser_infers_direction_from_balance_delta_for_a_single_amount_column()
    {
        const string csv = """
            Txn Date,Particulars,Amount,Balance
            01-04-2025,OPENING BALANCE,,100000.00
            02-04-2025,PAYMENT TO VENDOR,5000.00,95000.00
            03-04-2025,CUSTOMER RECEIPT NEFT,8000.00,103000.00
            """;

        var result = new BankStatementParser().Parse(Encoding.UTF8.GetBytes(csv), "text/csv", "s.csv");

        result.Lines.Should().HaveCount(2); // opening-balance band is skipped
        result.Lines.Single(l => l.Narration.Contains("VENDOR")).Debit.Should().Be(5000.00m);
        result.Lines.Single(l => l.Narration.Contains("RECEIPT")).Credit.Should().Be(8000.00m);
        result.PeriodFrom.Should().Be(new DateOnly(2025, 4, 2));
        result.PeriodTo.Should().Be(new DateOnly(2025, 4, 3));
    }

    // --------------------------------------------------------------------- test doubles

    private static AppDbContext NewContext(out AppDbContext db)
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        db = new AppDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    private sealed class FakeCurrentUser : ICurrentUser
    {
        public Guid UserId { get; } = Guid.Parse("11111111-1111-1111-1111-111111111111");
        public Guid TenantId { get; } = Guid.Parse("22222222-2222-2222-2222-222222222222");
        public IReadOnlyList<string> Roles { get; } = new[] { "User" };
        public bool IsAuthenticated => true;
        public Guid? SessionId => null;
        public bool IsInRole(string role) => Roles.Contains(role);
    }

    private sealed class FakeClock : IDateTime
    {
        public DateTimeOffset UtcNow { get; } = new(2025, 6, 1, 0, 0, 0, TimeSpan.Zero);
        public DateOnly TodayIst => DateOnly.FromDateTime(UtcNow.UtcDateTime);
    }

    private sealed class InMemoryFileStorage : IFileStorage
    {
        private readonly Dictionary<string, byte[]> _store = new();

        public Task<PresignedUpload> CreateUploadUrlAsync(string storageKey, string contentType, TimeSpan validFor, CancellationToken ct = default)
            => Task.FromResult(new PresignedUpload($"mem://{storageKey}", "PUT", new Dictionary<string, string>(), storageKey, DateTimeOffset.UtcNow.Add(validFor)));

        public Task<string> CreateDownloadUrlAsync(string storageKey, TimeSpan validFor, CancellationToken ct = default)
            => Task.FromResult($"mem://{storageKey}");

        public Task SaveAsync(string storageKey, byte[] content, string contentType, CancellationToken ct = default)
        {
            _store[storageKey] = content;
            return Task.CompletedTask;
        }

        public Task<byte[]?> ReadAsync(string storageKey, CancellationToken ct = default)
            => Task.FromResult(_store.TryGetValue(storageKey, out var bytes) ? bytes : null);

        public Task<bool> ExistsAsync(string storageKey, CancellationToken ct = default)
            => Task.FromResult(_store.ContainsKey(storageKey));

        public Task DeleteAsync(string storageKey, CancellationToken ct = default)
        {
            _store.Remove(storageKey);
            return Task.CompletedTask;
        }
    }
}
