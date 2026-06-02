using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TallyG.Tax.Api.Modules.TaxesPaid;
using TallyG.Tax.Domain.Abstractions;
using TallyG.Tax.Domain.Entities;
using TallyG.Tax.Domain.Enums;
using TallyG.Tax.Infrastructure.Persistence;
using Xunit;

namespace TallyG.Tax.Tests.Returns;

/// <summary>
/// Deductor-wise TDS + self-paid challans roll up onto the return's prepaid-tax fields so the refund
/// math reflects the itemised detail. Real Sqlite AppDbContext + the real TaxesPaidService.
/// </summary>
public sealed class TaxesPaidServiceTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TenantId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task Adding_tds_and_challans_rolls_up_onto_the_return_prepaid_taxes()
    {
        await using var db = NewContext();
        var ret = NewReturn();
        db.TaxReturns.Add(ret);
        await db.SaveChangesAsync();

        var svc = new TaxesPaidService(db, new FakeCurrentUser());

        await svc.AddTdsAsync(ret.Id, new UpsertTdsEntryRequest(
            TdsHead.Salary, "DELH12345A", "Acme Corp", null, 1_000_000m, 50_000m));
        await svc.AddTdsAsync(ret.Id, new UpsertTdsEntryRequest(
            TdsHead.OtherThanSalary, "MUMB54321Z", "HDFC Bank", "94A", 80_000m, 8_000m));
        await svc.AddChallanAsync(ret.Id, new UpsertChallanRequest(
            ChallanKind.Advance, "1234567", new DateOnly(2025, 12, 15), 12345, 15_000m));
        await svc.AddChallanAsync(ret.Id, new UpsertChallanRequest(
            ChallanKind.SelfAssessment, "0011223", new DateOnly(2026, 3, 25), 678, 5_000m));

        var updated = await db.TaxReturns.FirstAsync(r => r.Id == ret.Id);
        updated.TdsPaid.Should().Be(58_000m);              // salary 50k + other 8k
        updated.AdvanceTaxPaid.Should().Be(15_000m);
        updated.SelfAssessmentTaxPaid.Should().Be(5_000m);

        var summary = await svc.GetAsync(ret.Id);
        summary.TotalSalaryTds.Should().Be(50_000m);
        summary.TotalOtherTds.Should().Be(8_000m);
        summary.TotalTds.Should().Be(58_000m);
        summary.TotalPrepaid.Should().Be(78_000m);        // 58k TDS + 15k advance + 5k SAT
        summary.TdsEntries.Should().HaveCount(2);
        summary.Challans.Should().HaveCount(2);
    }

    [Fact]
    public async Task Deleting_a_challan_recomputes_the_rollup()
    {
        await using var db = NewContext();
        var ret = NewReturn();
        db.TaxReturns.Add(ret);
        await db.SaveChangesAsync();

        var svc = new TaxesPaidService(db, new FakeCurrentUser());
        var advance = await svc.AddChallanAsync(ret.Id, new UpsertChallanRequest(
            ChallanKind.Advance, "1234567", new DateOnly(2025, 12, 15), 12345, 15_000m));
        await svc.AddChallanAsync(ret.Id, new UpsertChallanRequest(
            ChallanKind.SelfAssessment, "0011223", new DateOnly(2026, 3, 25), 678, 5_000m));

        await svc.DeleteChallanAsync(ret.Id, advance.Id);

        var updated = await db.TaxReturns.FirstAsync(r => r.Id == ret.Id);
        updated.AdvanceTaxPaid.Should().Be(0m);
        updated.SelfAssessmentTaxPaid.Should().Be(5_000m);
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
        public Guid UserId => TaxesPaidServiceTests.UserId;
        public Guid TenantId => TaxesPaidServiceTests.TenantId;
        public IReadOnlyList<string> Roles => new[] { "User" };
        public bool IsAuthenticated => true;
        public Guid? SessionId => null;
        public bool IsInRole(string role) => true;
    }
}
