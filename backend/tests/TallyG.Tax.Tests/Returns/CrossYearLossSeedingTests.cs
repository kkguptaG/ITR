using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TallyG.Tax.Api.Modules.Returns;
using TallyG.Tax.Domain.Abstractions;
using TallyG.Tax.Domain.Entities;
using TallyG.Tax.Domain.Enums;
using TallyG.Tax.Domain.TaxEngine;
using TallyG.Tax.Infrastructure.Persistence;
using Xunit;

namespace TallyG.Tax.Tests.Returns;

/// <summary>
/// Cross-year carry-forward seeding: creating a return for AY N pre-fills its brought-forward loss
/// fields (s.71B/72/74) from the latest computed return of AY N-1. Real Sqlite AppDbContext + the
/// real ReturnService.CreateAsync path.
/// </summary>
public sealed class CrossYearLossSeedingTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TenantId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task New_return_seeds_brought_forward_losses_from_prior_year_carry_forward()
    {
        await using var db = NewContext();

        var priorAy = Ay("AY2025-26", new DateOnly(2025, 4, 1));
        var currentAy = Ay("AY2026-27", new DateOnly(2026, 4, 1));
        db.AssessmentYears.AddRange(priorAy, currentAy);

        var priorReturn = new TaxReturn
        {
            Id = Guid.NewGuid(), TenantId = TenantId, UserId = UserId, AssessmentYearId = priorAy.Id,
            ItrType = ItrType.ITR3, Status = ReturnStatus.Filed, RuleSetVersion = "1.0.0",
        };
        db.TaxReturns.Add(priorReturn);
        db.TaxComputations.Add(new TaxComputation
        {
            Id = Guid.NewGuid(), TenantId = TenantId, TaxReturnId = priorReturn.Id, Regime = Regime.New,
            IsRecommended = true, ComputedAt = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
            HousePropertyLossCarriedForward = 300_000m,
            BusinessLossCarriedForward = 190_000m,
            ShortTermCapitalLossCarriedForward = 200_000m,
            LongTermCapitalLossCarriedForward = 0m,
        });
        await db.SaveChangesAsync();

        var svc = NewService(db);
        var detail = await svc.CreateAsync(new CreateReturnRequest("AY2026-27", ItrType.ITR2, Regime.New));

        var created = await db.TaxReturns.FirstAsync(r => r.Id == detail.Id);
        created.BroughtForwardHousePropertyLoss.Should().Be(300_000m);
        created.BroughtForwardBusinessLoss.Should().Be(190_000m);
        created.BroughtForwardShortTermCapitalLoss.Should().Be(200_000m);
        created.BroughtForwardLongTermCapitalLoss.Should().Be(0m);
    }

    [Fact]
    public async Task New_return_with_no_prior_year_return_has_zero_brought_forward()
    {
        await using var db = NewContext();
        db.AssessmentYears.AddRange(Ay("AY2025-26", new DateOnly(2025, 4, 1)), Ay("AY2026-27", new DateOnly(2026, 4, 1)));
        await db.SaveChangesAsync();

        var svc = NewService(db);
        var detail = await svc.CreateAsync(new CreateReturnRequest("AY2026-27", ItrType.ITR1, Regime.New));

        var created = await db.TaxReturns.FirstAsync(r => r.Id == detail.Id);
        created.BroughtForwardHousePropertyLoss.Should().Be(0m);
        created.BroughtForwardBusinessLoss.Should().Be(0m);
    }

    private static AssessmentYear Ay(string code, DateOnly start) => new()
    {
        Id = Guid.NewGuid(), Code = code, FyCode = code, StartDate = start, EndDate = start.AddYears(1).AddDays(-1),
        DueDateNonAudit = new DateOnly(start.Year + 1, 7, 31), IsFilingOpen = true, RuleSetVersion = "1.0.0",
    };

    private static ReturnService NewService(AppDbContext db)
        => new(db, new FakeCurrentUser(), new FakeClock(), null!, null!, new TaxCalculator(), NullLogger<ReturnService>.Instance);

    private static AppDbContext NewContext()
    {
        var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options);
        db.Database.EnsureCreated();
        // This test isolates the carry-forward seeding LOGIC; relax FK enforcement so we don't have to
        // seed the full User/Tenant parent graph on the shared in-memory connection.
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "PRAGMA foreign_keys = OFF;";
            cmd.ExecuteNonQuery();
        }

        return db;
    }

    private sealed class FakeCurrentUser : ICurrentUser
    {
        public Guid UserId => CrossYearLossSeedingTests.UserId;
        public Guid TenantId => CrossYearLossSeedingTests.TenantId;
        public IReadOnlyList<string> Roles => new[] { "User" };
        public bool IsAuthenticated => true;
        public Guid? SessionId => null;
        public bool IsInRole(string role) => true;
    }

    private sealed class FakeClock : IDateTime
    {
        public DateTimeOffset UtcNow => new(2026, 6, 2, 0, 0, 0, TimeSpan.Zero);
        public DateOnly TodayIst => DateOnly.FromDateTime(UtcNow.UtcDateTime);
    }
}
