using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TallyG.Tax.Api.Modules.Accounting;
using TallyG.Tax.Domain.Abstractions;
using TallyG.Tax.Domain.Entities;
using TallyG.Tax.Domain.Enums;
using TallyG.Tax.Infrastructure.Persistence;
using Xunit;

namespace TallyG.Tax.Tests.Accounting;

/// <summary>
/// Financial statements (Balance Sheet + P&amp;L) derived from the double-entry books — the ITR-3
/// BS/P&amp;L source. Real Sqlite AppDbContext + the real FinancialStatementsService over seeded ledgers
/// and balanced vouchers.
/// </summary>
public sealed class FinancialStatementsTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TenantId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task Derives_pnl_and_balanced_balance_sheet_from_the_books()
    {
        await using var db = NewContext();

        var bank = Ledger("HDFC Bank", LedgerGroup.BankAccounts, isBank: true);
        var sales = Ledger("Sales", LedgerGroup.SalesIncome);
        var rent = Ledger("Rent", LedgerGroup.IndirectExpenses);
        db.Ledgers.AddRange(bank, sales, rent);

        // A sale (Dr Bank 1,00,000 / Cr Sales 1,00,000) and rent paid (Dr Rent 30,000 / Cr Bank 30,000).
        AddVoucher(db, (bank, DrCr.Debit, 100_000m), (sales, DrCr.Credit, 100_000m));
        AddVoucher(db, (rent, DrCr.Debit, 30_000m), (bank, DrCr.Credit, 30_000m));
        await db.SaveChangesAsync();

        var fs = await new FinancialStatementsService(db, new FakeCurrentUser()).GetAsync();

        fs.ProfitAndLoss.TotalIncome.Should().Be(100_000m);
        fs.ProfitAndLoss.TotalExpenses.Should().Be(30_000m);
        fs.ProfitAndLoss.NetProfit.Should().Be(70_000m);

        fs.BalanceSheet.TotalAssets.Should().Be(70_000m);                 // bank: 1,00,000 − 30,000
        fs.BalanceSheet.TotalLiabilitiesAndCapital.Should().Be(70_000m);  // net profit closed to capital
        fs.BalanceSheet.IsBalanced.Should().BeTrue();
    }

    private static Ledger Ledger(string name, LedgerGroup group, bool isBank = false) => new()
    {
        Id = Guid.NewGuid(), TenantId = TenantId, UserId = UserId, Name = name, Group = group, IsBank = isBank,
    };

    private static void AddVoucher(AppDbContext db, params (Ledger Ledger, DrCr Dir, decimal Amt)[] entries)
    {
        var v = new Voucher
        {
            Id = Guid.NewGuid(), TenantId = TenantId, UserId = UserId, Type = VoucherType.Journal,
            Date = new DateOnly(2025, 5, 1), Amount = entries.Sum(e => e.Amt) / 2,
        };
        db.Vouchers.Add(v);
        foreach (var (ledger, dir, amt) in entries)
        {
            db.VoucherEntries.Add(new VoucherEntry
            {
                Id = Guid.NewGuid(), TenantId = TenantId, VoucherId = v.Id, LedgerId = ledger.Id, Direction = dir, Amount = amt,
            });
        }
    }

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
        public Guid UserId => FinancialStatementsTests.UserId;
        public Guid TenantId => FinancialStatementsTests.TenantId;
        public IReadOnlyList<string> Roles => new[] { "User" };
        public bool IsAuthenticated => true;
        public Guid? SessionId => null;
        public bool IsInRole(string role) => true;
    }
}
