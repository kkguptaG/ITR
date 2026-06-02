using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TallyG.Tax.Api.Modules.AssetsLiabilities;
using TallyG.Tax.Domain.Abstractions;
using TallyG.Tax.Domain.Entities;
using TallyG.Tax.Domain.Enums;
using TallyG.Tax.Infrastructure.Persistence;
using Xunit;

namespace TallyG.Tax.Tests.AssetsLiabilities;

/// <summary>The Schedule AL declaration is one row per return, upserted. Real Sqlite + the real service.</summary>
public sealed class AssetsLiabilitiesServiceTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TenantId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task Upsert_persists_and_get_reflects_it_then_a_second_upsert_replaces()
    {
        await using var db = NewContext();
        var ret = NewReturn();
        db.TaxReturns.Add(ret);
        await db.SaveChangesAsync();

        var svc = new AssetsLiabilitiesService(db, new FakeCurrentUser());

        (await svc.GetAsync(ret.Id)).BankDeposits.Should().Be(0m, "no declaration yet → zeros");

        await svc.UpsertAsync(ret.Id, new UpsertAssetsLiabilitiesRequest(
            BankDeposits: 500_000m, SharesAndSecurities: 300_000m, InsurancePolicies: 0m, LoansAndAdvancesGiven: 0m,
            CashInHand: 50_000m, JewelleryBullion: 200_000m, ArtCollections: 0m, Vehicles: 800_000m, Liabilities: 400_000m));

        var got = await svc.GetAsync(ret.Id);
        got.BankDeposits.Should().Be(500_000m);
        got.Vehicles.Should().Be(800_000m);
        got.Liabilities.Should().Be(400_000m);

        // A second upsert replaces (still one row).
        await svc.UpsertAsync(ret.Id, new UpsertAssetsLiabilitiesRequest(
            BankDeposits: 600_000m, SharesAndSecurities: 0m, InsurancePolicies: 0m, LoansAndAdvancesGiven: 0m,
            CashInHand: 0m, JewelleryBullion: 0m, ArtCollections: 0m, Vehicles: 0m, Liabilities: 0m));

        (await svc.GetAsync(ret.Id)).BankDeposits.Should().Be(600_000m);
        (await db.AssetsLiabilities.CountAsync(a => a.TaxReturnId == ret.Id)).Should().Be(1);
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
        public Guid UserId => AssetsLiabilitiesServiceTests.UserId;
        public Guid TenantId => AssetsLiabilitiesServiceTests.TenantId;
        public IReadOnlyList<string> Roles => new[] { "User" };
        public bool IsAuthenticated => true;
        public Guid? SessionId => null;
        public bool IsInRole(string role) => true;
    }
}
