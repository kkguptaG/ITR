using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using TallyG.Tax.Api.Modules.EVerify;
using TallyG.Tax.Domain.Abstractions;
using TallyG.Tax.Domain.Common;
using TallyG.Tax.Domain.Entities;
using TallyG.Tax.Domain.Enums;
using TallyG.Tax.Infrastructure.Persistence;
using TallyG.Tax.Infrastructure.Services;
using Xunit;

namespace TallyG.Tax.Tests.Returns;

/// <summary>
/// Post-filing e-verification: real Sqlite AppDbContext + the real EVerificationService over the
/// deterministic MockEVerificationClient. Covers all six modes, the 30-day window, idempotency, and
/// the attempt/expiry guards.
/// </summary>
public sealed class EVerificationServiceTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TenantId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTimeOffset FiledAt = new(2026, 6, 1, 9, 0, 0, TimeSpan.Zero);

    // ----------------------------------------------------------------- Aadhaar OTP

    [Fact]
    public async Task AadhaarOtp_start_then_confirm_with_dev_code_verifies_the_return()
    {
        await using var db = NewContext();
        var ret = await SeedFiledReturnAsync(db);
        var svc = NewService(db);

        var start = await svc.StartAsync(ret.Id, new EVerificationStartRequest(EVerificationMode.AadhaarOtp));

        start.RequiresCode.Should().BeTrue();
        start.DevCode.Should().NotBeNullOrEmpty();
        start.TransactionId.Should().NotBeNullOrEmpty();
        start.ChallengeExpiresAt.Should().Be(FiledAt.AddSeconds(15 * 60));

        var status = await svc.ConfirmAsync(ret.Id, new EVerificationConfirmRequest(start.DevCode));

        status.IsVerified.Should().BeTrue();
        status.Status.Should().Be(EVerificationStatus.Verified);
        status.EvcReference.Should().StartWith("AADHAAROTP-");
        status.VerifiedAt.Should().Be(FiledAt);

        var reloaded = await db.TaxReturns.FirstAsync(r => r.Id == ret.Id);
        reloaded.EVerifiedAt.Should().Be(FiledAt);
    }

    [Fact]
    public async Task AadhaarOtp_wrong_code_increments_attempts_then_locks_after_max()
    {
        await using var db = NewContext();
        var ret = await SeedFiledReturnAsync(db);
        var svc = NewService(db);
        await svc.StartAsync(ret.Id, new EVerificationStartRequest(EVerificationMode.AadhaarOtp));

        for (var i = 0; i < 5; i++)
        {
            var bad = await Assert.ThrowsAsync<AppException>(() =>
                svc.ConfirmAsync(ret.Id, new EVerificationConfirmRequest("000000")));
            bad.Code.Should().Be("EVERIFY.INVALID_CODE");
        }

        var locked = await Assert.ThrowsAsync<AppException>(() =>
            svc.ConfirmAsync(ret.Id, new EVerificationConfirmRequest("000000")));
        locked.Code.Should().Be("EVERIFY.LOCKED");

        var ev = await db.EVerifications.FirstAsync(e => e.TaxReturnId == ret.Id);
        ev.Status.Should().Be(EVerificationStatus.Failed);
        var reloaded = await db.TaxReturns.FirstAsync(r => r.Id == ret.Id);
        reloaded.EVerifiedAt.Should().BeNull();
    }

    [Fact]
    public async Task Confirm_after_challenge_expiry_throws_and_marks_expired()
    {
        await using var db = NewContext();
        var ret = await SeedFiledReturnAsync(db);
        var clock = new FakeClock(FiledAt);
        var svc = NewService(db, clock);
        var start = await svc.StartAsync(ret.Id, new EVerificationStartRequest(EVerificationMode.AadhaarOtp));

        clock.Now = FiledAt.AddSeconds(15 * 60 + 1); // one second past the OTP window

        var ex = await Assert.ThrowsAsync<AppException>(() =>
            svc.ConfirmAsync(ret.Id, new EVerificationConfirmRequest(start.DevCode)));
        ex.Code.Should().Be("EVERIFY.EXPIRED");

        var ev = await db.EVerifications.FirstAsync(e => e.TaxReturnId == ret.Id);
        ev.Status.Should().Be(EVerificationStatus.Expired);
    }

    // ----------------------------------------------------------------- net-banking (no code)

    [Fact]
    public async Task NetBanking_requires_no_code_and_confirms_immediately()
    {
        await using var db = NewContext();
        var ret = await SeedFiledReturnAsync(db);
        var svc = NewService(db);

        var start = await svc.StartAsync(ret.Id, new EVerificationStartRequest(EVerificationMode.NetBanking));
        start.RequiresCode.Should().BeFalse();
        start.DevCode.Should().BeNull();

        var status = await svc.ConfirmAsync(ret.Id, new EVerificationConfirmRequest(null));
        status.IsVerified.Should().BeTrue();
        status.EvcReference.Should().StartWith("NETBANK-");
    }

    // ----------------------------------------------------------------- EVC family

    [Theory]
    [InlineData(EVerificationMode.BankAccountEvc)]
    [InlineData(EVerificationMode.DematEvc)]
    [InlineData(EVerificationMode.BankAtmEvc)]
    public async Task Evc_modes_start_then_confirm_verify_the_return(EVerificationMode mode)
    {
        await using var db = NewContext();
        var ret = await SeedFiledReturnAsync(db);
        var svc = NewService(db);

        var start = await svc.StartAsync(ret.Id, new EVerificationStartRequest(mode));
        start.RequiresCode.Should().BeTrue();
        start.ChallengeExpiresAt.Should().Be(FiledAt.AddSeconds(72 * 60 * 60)); // 72-hour EVC window

        var status = await svc.ConfirmAsync(ret.Id, new EVerificationConfirmRequest(start.DevCode));
        status.IsVerified.Should().BeTrue();
        status.EvcReference.Should().StartWith("EVC-");
    }

    // ----------------------------------------------------------------- ITR-V (postal)

    [Fact]
    public async Task ItrV_dispatch_then_status_poll_reconciles_cpc_receipt()
    {
        await using var db = NewContext();
        var ret = await SeedFiledReturnAsync(db);
        var svc = NewService(db);

        var start = await svc.StartAsync(ret.Id, new EVerificationStartRequest(EVerificationMode.ItrV));
        start.RequiresCode.Should().BeFalse();
        start.Status.Should().Be(EVerificationStatus.Pending);
        start.Instruction.Should().Contain("Bengaluru");

        var dispatched = await db.EVerifications.FirstAsync(e => e.TaxReturnId == ret.Id);
        dispatched.ItrvDispatchedAt.Should().Be(FiledAt);

        // The user cannot enter a code for the postal route.
        var noCode = await Assert.ThrowsAsync<AppException>(() =>
            svc.ConfirmAsync(ret.Id, new EVerificationConfirmRequest("123456")));
        noCode.Code.Should().Be("EVERIFY.ITRV_NO_CODE");

        // The status read reconciles CPC receipt (stub reports received).
        var status = await svc.GetAsync(ret.Id);
        status.IsVerified.Should().BeTrue();
        status.EvcReference.Should().StartWith("ITRV-");

        var ev = await db.EVerifications.FirstAsync(e => e.TaxReturnId == ret.Id);
        ev.ItrvReceivedAt.Should().NotBeNull();
    }

    // ----------------------------------------------------------------- guards / window / idempotency

    [Fact]
    public async Task Start_on_an_unfiled_return_is_rejected()
    {
        await using var db = NewContext();
        var ret = await SeedFiledReturnAsync(db, status: ReturnStatus.Paid, submittedAt: null);
        var svc = NewService(db);

        var ex = await Assert.ThrowsAsync<AppException>(() =>
            svc.StartAsync(ret.Id, new EVerificationStartRequest(EVerificationMode.AadhaarOtp)));
        ex.Code.Should().Be("EVERIFY.NOT_FILED");
    }

    [Fact]
    public async Task Already_verified_return_is_idempotent_and_offers_no_modes()
    {
        await using var db = NewContext();
        var ret = await SeedFiledReturnAsync(db);
        var svc = NewService(db);
        await svc.StartAsync(ret.Id, new EVerificationStartRequest(EVerificationMode.NetBanking));
        await svc.ConfirmAsync(ret.Id, new EVerificationConfirmRequest(null));

        // Re-start returns the verified state without issuing a fresh challenge.
        var restart = await svc.StartAsync(ret.Id, new EVerificationStartRequest(EVerificationMode.AadhaarOtp));
        restart.Status.Should().Be(EVerificationStatus.Verified);
        restart.TransactionId.Should().BeNull();

        var status = await svc.GetAsync(ret.Id);
        status.IsVerified.Should().BeTrue();
        status.AvailableModes.Should().BeEmpty();
    }

    [Fact]
    public async Task Status_reports_the_thirty_day_window_for_a_filed_unverified_return()
    {
        await using var db = NewContext();
        var ret = await SeedFiledReturnAsync(db);
        // "Today" is 10 days after filing → 20 days remain in the 30-day window.
        var svc = NewService(db, new FakeClock(FiledAt.AddDays(10)));

        var status = await svc.GetAsync(ret.Id);

        status.IsFiled.Should().BeTrue();
        status.IsVerified.Should().BeFalse();
        status.VerifyBy.Should().Be(new DateOnly(2026, 7, 1)); // 01-Jun + 30 days
        status.DaysRemaining.Should().Be(20);
        status.IsOverdue.Should().BeFalse();
        status.AvailableModes.Should().HaveCount(6);
    }

    [Fact]
    public async Task Status_flags_overdue_after_the_window_lapses()
    {
        await using var db = NewContext();
        var ret = await SeedFiledReturnAsync(db);
        var svc = NewService(db, new FakeClock(FiledAt.AddDays(45)));

        var status = await svc.GetAsync(ret.Id);
        status.DaysRemaining.Should().Be(-15);
        status.IsOverdue.Should().BeTrue();
    }

    // ----------------------------------------------------------------- harness

    private static async Task<TaxReturn> SeedFiledReturnAsync(
        AppDbContext db, ReturnStatus status = ReturnStatus.Filed, DateTimeOffset? submittedAt = null)
    {
        var ay = new AssessmentYear
        {
            Id = Guid.NewGuid(), Code = "AY2026-27", FyCode = "AY2026-27",
            StartDate = new DateOnly(2026, 4, 1), EndDate = new DateOnly(2027, 3, 31),
            DueDateNonAudit = new DateOnly(2027, 7, 31), IsFilingOpen = true, RuleSetVersion = "1.0.0",
        };
        db.AssessmentYears.Add(ay);

        var ret = new TaxReturn
        {
            Id = Guid.NewGuid(), TenantId = TenantId, UserId = UserId, AssessmentYearId = ay.Id,
            ItrType = ItrType.ITR1, Status = status, RuleSetVersion = "1.0.0",
            AcknowledgmentNumber = status is ReturnStatus.Filed or ReturnStatus.Processed ? "123456789012345" : null,
            SubmittedAt = submittedAt ?? (status is ReturnStatus.Filed or ReturnStatus.Processed ? FiledAt : null),
        };
        db.TaxReturns.Add(ret);
        db.Users.Add(new User { Id = UserId, TenantId = TenantId, FullName = "Test Filer", MobileE164 = "+919876543210", PanMasked = "ABCXXXXX1F" });
        await db.SaveChangesAsync();
        return ret;
    }

    private static EVerificationService NewService(AppDbContext db, FakeClock? clock = null)
        => new(db, new FakeCurrentUser(), clock ?? new FakeClock(FiledAt),
            new MockEVerificationClient(NullLogger<MockEVerificationClient>.Instance),
            new FakeHostEnvironment(), NullLogger<EVerificationService>.Instance);

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
        public Guid UserId => EVerificationServiceTests.UserId;
        public Guid TenantId => EVerificationServiceTests.TenantId;
        public IReadOnlyList<string> Roles => new[] { "User" };
        public bool IsAuthenticated => true;
        public Guid? SessionId => null;
        public bool IsInRole(string role) => true;
    }

    private sealed class FakeClock : IDateTime
    {
        public FakeClock(DateTimeOffset now) => Now = now;
        public DateTimeOffset Now { get; set; }
        public DateTimeOffset UtcNow => Now;
        public DateOnly TodayIst => DateOnly.FromDateTime(Now.ToOffset(TimeSpan.FromHours(5.5)).Date);
    }

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; }
            = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
