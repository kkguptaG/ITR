using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TallyG.Tax.Api.Modules.Refunds;
using TallyG.Tax.Api.Modules.Returns;
using TallyG.Tax.Domain.Abstractions;
using TallyG.Tax.Domain.Common;
using TallyG.Tax.Domain.Entities;
using TallyG.Tax.Domain.Enums;
using TallyG.Tax.Infrastructure.Persistence;
using TallyG.Tax.Infrastructure.Services;
using Xunit;

namespace TallyG.Tax.Tests.Returns;

/// <summary>
/// Post-processing refund tracking: real Sqlite AppDbContext + the real RefundService over the real
/// ReturnService (processing reconcile) and the deterministic MockRefundStatusClient. Covers the
/// refund / payable / nil branches, the determined → sent → paid progression, the not-yet-processed
/// gate, and the failed → re-issue path.
/// </summary>
public sealed class RefundServiceTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TenantId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTimeOffset Now = new(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);

    // ----------------------------------------------------------------- refund-due lifecycle

    [Fact]
    public async Task Refund_due_return_progresses_determined_then_sent_then_paid()
    {
        await using var db = NewContext();
        var ret = await SeedProcessedReturnAsync(db, refundOrPayable: 15_000m);
        await SeedRefundAccountAsync(db);
        var svc = NewService(db);

        var s1 = await svc.GetAsync(ret.Id);
        s1.IsProcessed.Should().BeTrue();
        s1.Status.Should().Be(RefundStatus.RefundDetermined);
        s1.DeterminedAmount.Should().Be(15_000m);
        s1.DemandAmount.Should().Be(0m);
        s1.IntimationDate.Should().NotBeNull();

        var s2 = await svc.GetAsync(ret.Id);
        s2.Status.Should().Be(RefundStatus.RefundSentToBank);

        var s3 = await svc.GetAsync(ret.Id);
        s3.Status.Should().Be(RefundStatus.RefundPaid);
        s3.Mode.Should().Be("ECS");
        s3.RefundSequenceNo.Should().StartWith("CMP");
        s3.PaidAt.Should().NotBeNull();
        s3.CreditedAccountLast4.Should().Be("6789");
        s3.RefundBankName.Should().Be("HDFC Bank");

        // Terminal: a further read does not change a paid refund.
        var s4 = await svc.GetAsync(ret.Id);
        s4.Status.Should().Be(RefundStatus.RefundPaid);
        s4.PaidAt.Should().Be(s3.PaidAt);
    }

    [Fact]
    public async Task Payable_return_yields_a_demand()
    {
        await using var db = NewContext();
        var ret = await SeedProcessedReturnAsync(db, refundOrPayable: -8_000m);
        var svc = NewService(db);

        var s = await svc.GetAsync(ret.Id);
        s.Status.Should().Be(RefundStatus.DemandDetermined);
        s.DemandAmount.Should().Be(8_000m);
        s.DeterminedAmount.Should().Be(0m);
        s.CanReissue.Should().BeFalse();
    }

    [Fact]
    public async Task Nil_return_yields_no_refund_or_demand()
    {
        await using var db = NewContext();
        var ret = await SeedProcessedReturnAsync(db, refundOrPayable: 0m);
        var svc = NewService(db);

        var s = await svc.GetAsync(ret.Id);
        s.Status.Should().Be(RefundStatus.NoRefundOrDemand);
        s.DeterminedAmount.Should().Be(0m);
        s.DemandAmount.Should().Be(0m);
    }

    // ----------------------------------------------------------------- processing gate

    [Fact]
    public async Task Unprocessed_return_is_not_determined()
    {
        await using var db = NewContext();
        // Filed but NOT e-verified -> the status reconcile leaves it Filed, so no refund is determined.
        var ret = await SeedReturnAsync(db, ReturnStatus.Filed, eVerified: false, refundOrPayable: 12_000m);
        var svc = NewService(db);

        var s = await svc.GetAsync(ret.Id);
        s.IsProcessed.Should().BeFalse();
        s.Status.Should().Be(RefundStatus.NotDetermined);
        (await db.RefundTrackings.AnyAsync(r => r.TaxReturnId == ret.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task Filed_and_everified_return_auto_advances_to_processed_then_determines_refund()
    {
        await using var db = NewContext();
        // Filed + e-verified: the refund read reconciles it to Processed (via ReturnService + the
        // fake ERI) and then determines the refund.
        var ret = await SeedReturnAsync(db, ReturnStatus.Filed, eVerified: true, refundOrPayable: 9_000m);
        var svc = NewService(db);

        var s = await svc.GetAsync(ret.Id);
        s.IsProcessed.Should().BeTrue();
        s.Status.Should().Be(RefundStatus.RefundDetermined);
        s.DeterminedAmount.Should().Be(9_000m);

        (await db.TaxReturns.FirstAsync(r => r.Id == ret.Id)).Status.Should().Be(ReturnStatus.Processed);
    }

    // ----------------------------------------------------------------- re-issue

    [Fact]
    public async Task Reissue_from_failed_resets_to_determined_then_repays()
    {
        await using var db = NewContext();
        var ret = await SeedProcessedReturnAsync(db, refundOrPayable: 20_000m);
        await SeedRefundAccountAsync(db);
        db.RefundTrackings.Add(new RefundTracking
        {
            TenantId = TenantId, TaxReturnId = ret.Id, Status = RefundStatus.RefundFailed,
            DeterminedAmount = 20_000m, FailureReason = "Account closed",
        });
        await db.SaveChangesAsync();
        var svc = NewService(db);

        var reissued = await svc.RequestReissueAsync(ret.Id);
        reissued.Status.Should().Be(RefundStatus.RefundDetermined);
        reissued.ReissueCount.Should().Be(1);
        reissued.FailureReason.Should().BeNull();

        // The next reads disburse again.
        (await svc.GetAsync(ret.Id)).Status.Should().Be(RefundStatus.RefundSentToBank);
        (await svc.GetAsync(ret.Id)).Status.Should().Be(RefundStatus.RefundPaid);
    }

    [Fact]
    public async Task Reissue_when_not_failed_is_rejected()
    {
        await using var db = NewContext();
        var ret = await SeedProcessedReturnAsync(db, refundOrPayable: 5_000m);
        var svc = NewService(db);
        await svc.GetAsync(ret.Id); // -> RefundDetermined

        var ex = await Assert.ThrowsAsync<AppException>(() => svc.RequestReissueAsync(ret.Id));
        ex.Code.Should().Be("REFUND.NOT_FAILED");
    }

    // ----------------------------------------------------------------- harness

    private static async Task<TaxReturn> SeedProcessedReturnAsync(AppDbContext db, decimal refundOrPayable)
        => await SeedReturnAsync(db, ReturnStatus.Processed, eVerified: true, refundOrPayable: refundOrPayable);

    private static async Task<TaxReturn> SeedReturnAsync(
        AppDbContext db, ReturnStatus status, bool eVerified, decimal refundOrPayable)
    {
        var ay = new AssessmentYear
        {
            Id = Guid.NewGuid(), Code = "AY2026-27", FyCode = "AY2026-27",
            StartDate = new DateOnly(2026, 4, 1), EndDate = new DateOnly(2027, 3, 31),
            DueDateNonAudit = new DateOnly(2027, 7, 31), IsFilingOpen = true, RuleSetVersion = "1.0.0",
        };
        db.AssessmentYears.Add(ay);

        var filedish = status is ReturnStatus.Filed or ReturnStatus.Processed;
        var ret = new TaxReturn
        {
            Id = Guid.NewGuid(), TenantId = TenantId, UserId = UserId, AssessmentYearId = ay.Id,
            ItrType = ItrType.ITR1, Status = status, RuleSetVersion = "1.0.0",
            AcknowledgmentNumber = filedish ? "123456789012345" : null,
            SubmittedAt = filedish ? Now.AddDays(-40) : null,
            EVerifiedAt = eVerified ? Now.AddDays(-39) : null,
        };
        db.TaxReturns.Add(ret);
        db.TaxComputations.Add(new TaxComputation
        {
            Id = Guid.NewGuid(), TenantId = TenantId, TaxReturnId = ret.Id, Regime = Regime.New,
            IsRecommended = true, ComputedAt = Now.AddDays(-41), RefundOrPayable = refundOrPayable,
        });
        await db.SaveChangesAsync();
        return ret;
    }

    private static async Task SeedRefundAccountAsync(AppDbContext db)
    {
        db.BankAccountDetails.Add(new BankAccountDetail
        {
            Id = Guid.NewGuid(), TenantId = TenantId, UserId = UserId,
            BankName = "HDFC Bank", AccountNumber = "000123456789", Ifsc = "HDFC0001234",
            AccountType = "SB", UseForRefund = true,
        });
        await db.SaveChangesAsync();
    }

    private static RefundService NewService(AppDbContext db)
    {
        var clock = new FakeClock();
        var returns = new ReturnService(
            db, new FakeCurrentUser(), clock, new FakeEFilingClient(), null!, null!, NullLogger<ReturnService>.Instance);
        return new RefundService(
            db, new FakeCurrentUser(), clock, new MockRefundStatusClient(NullLogger<MockRefundStatusClient>.Instance),
            returns, NullLogger<RefundService>.Instance);
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
        public Guid UserId => RefundServiceTests.UserId;
        public Guid TenantId => RefundServiceTests.TenantId;
        public IReadOnlyList<string> Roles => new[] { "User" };
        public bool IsAuthenticated => true;
        public Guid? SessionId => null;
        public bool IsInRole(string role) => true;
    }

    private sealed class FakeClock : IDateTime
    {
        public DateTimeOffset UtcNow => Now;
        public DateOnly TodayIst => DateOnly.FromDateTime(Now.UtcDateTime);
    }

    private sealed class FakeEFilingClient : IEFilingClient
    {
        public Task<EFilingResult> SubmitAsync(Guid taxReturnId, string ay, string itrJson, CancellationToken ct = default)
            => Task.FromResult(new EFilingResult(true, "123456789012345", null, null, Now));

        public Task<EFilingResult> GetStatusAsync(string ack, CancellationToken ct = default)
            => Task.FromResult(new EFilingResult(true, ack, null, null, Now));
    }
}
