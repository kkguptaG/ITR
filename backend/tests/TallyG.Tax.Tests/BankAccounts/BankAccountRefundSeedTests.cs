using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TallyG.Tax.Api.Modules.BankAccounts;
using TallyG.Tax.Domain.Abstractions;
using TallyG.Tax.Infrastructure.Persistence;
using Xunit;

namespace TallyG.Tax.Tests.BankAccounts;

/// <summary>
/// The demo seed must satisfy the single-refund-account invariant: exactly one of the demo user's
/// bank accounts is flagged UseForRefund. The seed inserts accounts directly from several methods
/// (the base accounts + the per-return seeds), bypassing the BankAccountService that normally keeps
/// the flag unique — <see cref="DbInitializer.NormalizeRefundAccountsAsync"/> backstops it. These run
/// the real DbInitializer.SeedAsync on a fresh Sqlite AppDbContext (the live demo path), then assert
/// both the raw rows and the BankAccountService.ListAsync that GET /api/v1/bank-accounts delegates to.
/// </summary>
public sealed class BankAccountRefundSeedTests
{
    [Fact]
    public async Task Seed_flags_exactly_one_demo_refund_account_the_primary_hdfc()
    {
        await using var db = NewContext();
        await DbInitializer.SeedAsync(db, NullLogger.Instance);

        var accounts = await db.BankAccountDetails
            .Where(b => b.UserId == DbInitializer.DemoUserId)
            .ToListAsync();

        // More than one account on file (so the invariant is non-trivial)…
        accounts.Count.Should().BeGreaterThan(1);
        // …yet exactly one is the refund account, and it is the primary HDFC ...6789.
        accounts.Should().ContainSingle(b => b.UseForRefund)
            .Which.AccountNumber.Should().Be("50100123456789");
    }

    [Fact]
    public async Task ListAsync_returns_exactly_one_refund_account_for_the_demo_user()
    {
        await using var db = NewContext();
        await DbInitializer.SeedAsync(db, NullLogger.Instance);

        // The exact path GET /api/v1/bank-accounts runs (controller → service.ListAsync).
        var svc = new BankAccountService(db, new DemoCurrentUser());
        var list = await svc.ListAsync();

        list.Should().ContainSingle(b => b.UseForRefund)
            .Which.AccountNumberMasked.Should().EndWith("6789");
    }

    private static AppDbContext NewContext()
    {
        // A real, file-less Sqlite database — the same provider the no-infra demo boots on. The open
        // connection is held by the context for its lifetime, keeping the in-memory DB alive.
        var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options);
        db.Database.EnsureCreated();
        return db;
    }

    private sealed class DemoCurrentUser : ICurrentUser
    {
        public Guid UserId => DbInitializer.DemoUserId;
        public Guid TenantId => DbInitializer.RetailTenantId;
        public IReadOnlyList<string> Roles => new[] { "User" };
        public bool IsAuthenticated => true;
        public Guid? SessionId => null;
        public bool IsInRole(string role) => true;
    }
}
