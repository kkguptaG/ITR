using Microsoft.EntityFrameworkCore;
using TallyG.Tax.Domain.Abstractions;
using TallyG.Tax.Domain.Common;
using TallyG.Tax.Domain.Entities;
using TallyG.Tax.Domain.Enums;
using TallyG.Tax.Infrastructure.Persistence;
using TallyG.Tax.Api.Common;

namespace TallyG.Tax.Api.Modules.EVerify;

/// <summary>
/// E-verification application service (docs 04). A return filed with the ITD is not legally valid
/// until it is verified within <see cref="VerificationWindowDays"/> days (CBDT Notn. 5/2022). Five
/// modes complete electronically through the <see cref="IEVerificationClient"/> ACL (Aadhaar OTP,
/// net-banking, and bank-account/demat/bank-ATM EVC); the sixth posts a signed ITR-V to CPC, which we
/// reconcile on read. Success stamps <see cref="TaxReturn.EVerifiedAt"/> — the single source of truth
/// every other read (status, ITR-V acknowledgement) already keys off.
///
/// Every row is scoped to the current tenant + user; another user's return is indistinguishable from
/// absent (404). Auto-registered by Scrutor (EVerificationService : IEVerificationService, scoped).
/// </summary>
public sealed class EVerificationService : IEVerificationService
{
    /// <summary>Days from filing within which the return must be e-verified (CBDT Notn. 5/2022, w.e.f. 01-Aug-2022).</summary>
    public const int VerificationWindowDays = 30;

    private static readonly IReadOnlyList<EVerificationMode> AllModes = new[]
    {
        EVerificationMode.AadhaarOtp,
        EVerificationMode.NetBanking,
        EVerificationMode.BankAccountEvc,
        EVerificationMode.DematEvc,
        EVerificationMode.BankAtmEvc,
        EVerificationMode.ItrV
    };

    private static readonly IReadOnlyList<EVerificationMode> NoModes = Array.Empty<EVerificationMode>();

    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTime _clock;
    private readonly IEVerificationClient _client;
    private readonly IHostEnvironment _env;
    private readonly ILogger<EVerificationService> _logger;

    public EVerificationService(
        AppDbContext db,
        ICurrentUser currentUser,
        IDateTime clock,
        IEVerificationClient client,
        IHostEnvironment env,
        ILogger<EVerificationService> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _client = client;
        _env = env;
        _logger = logger;
    }

    // =========================================================== status

    public async Task<EVerificationStatusDto> GetAsync(Guid returnId, CancellationToken ct = default)
    {
        var ret = await LoadOwnedReturnAsync(returnId, ct);
        var ev = await _db.EVerifications.FirstOrDefaultAsync(e => e.TaxReturnId == returnId, ct);

        // Reconcile a posted ITR-V: CPC receipt is asynchronous, so a pending ITR-V is polled on read
        // (mirrors ReturnService.GetStatusAsync's ERI-processing reconcile).
        if (ret.EVerifiedAt is null && ev is { Mode: EVerificationMode.ItrV, Status: EVerificationStatus.Pending }
            && ev.ItrvDispatchedAt is not null && !string.IsNullOrEmpty(ev.TransactionId))
        {
            var poll = await _client.PollItrvAsync(ev.TransactionId, ct);
            if (poll.Success)
            {
                MarkVerified(ret, ev, poll.EvcReference);
                ev.ItrvReceivedAt = _clock.UtcNow;
                await _db.SaveChangesAsync(ct);
                _logger.LogInformation("ITR-V receipt reconciled for return {ReturnId}", returnId);
            }
        }

        return BuildStatus(ret, ev);
    }

    // =========================================================== start

    public async Task<EVerificationStartResponse> StartAsync(Guid returnId, EVerificationStartRequest request, CancellationToken ct = default)
    {
        if (!Enum.IsDefined(request.Mode))
        {
            throw AppException.Validation("Unknown e-verification mode.", "EVERIFY.MODE_UNKNOWN");
        }

        var ret = await LoadOwnedReturnAsync(returnId, ct);
        EnsureFiled(ret);

        var ev = await _db.EVerifications.FirstOrDefaultAsync(e => e.TaxReturnId == returnId, ct);

        // Idempotent: an already-verified return reports its verified state without issuing a challenge.
        if (ret.EVerifiedAt is not null)
        {
            return new EVerificationStartResponse(
                ret.Id, ev?.Mode ?? request.Mode, EVerificationStatus.Verified,
                null, null, RequiresCode: false,
                "This return is already e-verified — no further action needed.", DevCode: null);
        }

        var now = _clock.UtcNow;
        if (ev is null)
        {
            ev = NewRow(ret);
            _db.EVerifications.Add(ev);
        }
        ev.Mode = request.Mode;
        ev.Status = EVerificationStatus.Pending;
        ev.Attempts = 0;
        ev.FailureReason = null;
        ev.EvcReference = null;
        ev.VerifiedAt = null;

        EVerificationStartResponse response;

        if (request.Mode == EVerificationMode.ItrV)
        {
            // Postal route: no electronic challenge — dispatch the ITR-V and await CPC receipt.
            ev.TransactionId = $"ITRV-{ret.Id:N}";
            ev.ChallengeExpiresAt = null;
            ev.ItrvDispatchedAt = now;
            ev.ItrvReceivedAt = null;

            response = new EVerificationStartResponse(
                ret.Id, ev.Mode, ev.Status, ev.TransactionId, null, RequiresCode: false,
                "Download your ITR-V, sign it in blue ink, and post it to: Centralised Processing Centre, "
                + "Income Tax Department, Bengaluru – 560500, within "
                + $"{VerificationWindowDays} days of filing. We'll mark the return verified once CPC records receipt.",
                DevCode: null);
        }
        else
        {
            var ctx = await BuildContextAsync(ret, ct);
            var challenge = await _client.StartAsync(request.Mode, ctx, ct);

            ev.TransactionId = challenge.TransactionId;
            ev.ChallengeExpiresAt = now.AddSeconds(challenge.TtlSeconds);
            ev.ItrvDispatchedAt = null;
            ev.ItrvReceivedAt = null;

            response = new EVerificationStartResponse(
                ret.Id, ev.Mode, ev.Status, ev.TransactionId, ev.ChallengeExpiresAt,
                RequiresCode: request.Mode != EVerificationMode.NetBanking,
                challenge.Instruction,
                // The dev OTP/EVC is surfaced ONLY in Development (mirrors the Auth devOtp contract).
                DevCode: _env.IsDevelopment() ? challenge.DevCode : null);
        }

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Started {Mode} e-verification for return {ReturnId}", request.Mode, returnId);
        return response;
    }

    // =========================================================== confirm

    public async Task<EVerificationStatusDto> ConfirmAsync(Guid returnId, EVerificationConfirmRequest request, CancellationToken ct = default)
    {
        var ret = await LoadOwnedReturnAsync(returnId, ct);
        EnsureFiled(ret);

        // Idempotent: already verified.
        if (ret.EVerifiedAt is not null)
        {
            var verified = await _db.EVerifications.FirstOrDefaultAsync(e => e.TaxReturnId == returnId, ct);
            return BuildStatus(ret, verified);
        }

        var ev = await _db.EVerifications.FirstOrDefaultAsync(e => e.TaxReturnId == returnId, ct)
                 ?? throw AppException.Validation("Start e-verification before submitting a code.", "EVERIFY.NO_CHALLENGE");

        if (ev.Mode == EVerificationMode.ItrV)
        {
            throw AppException.Validation(
                "ITR-V is verified when CPC records your posted form — there is no code to enter. Track its status instead.",
                "EVERIFY.ITRV_NO_CODE");
        }

        var now = _clock.UtcNow;

        if (ev.ChallengeExpiresAt is { } expiry && now >= expiry)
        {
            ev.Status = EVerificationStatus.Expired;
            ev.FailureReason = "The verification code expired.";
            await _db.SaveChangesAsync(ct);
            throw new AppException("EVERIFY.EXPIRED", "The verification code has expired. Please start again.", 409);
        }

        if (ev.Attempts >= ev.MaxAttempts)
        {
            ev.Status = EVerificationStatus.Failed;
            await _db.SaveChangesAsync(ct);
            throw new AppException("EVERIFY.LOCKED", "Too many incorrect attempts. Please start verification again.", 429);
        }

        var code = request.Code?.Trim();
        if (ev.Mode != EVerificationMode.NetBanking && string.IsNullOrEmpty(code))
        {
            throw AppException.Validation("Enter the verification code.", "EVERIFY.CODE_REQUIRED");
        }

        var outcome = await _client.ConfirmAsync(ev.Mode, ev.TransactionId ?? string.Empty, code, ct);

        if (!outcome.Success)
        {
            ev.Attempts++;
            ev.FailureReason = outcome.FailureReason;
            if (ev.Attempts >= ev.MaxAttempts)
            {
                ev.Status = EVerificationStatus.Failed;
            }
            await _db.SaveChangesAsync(ct);
            throw new AppException("EVERIFY.INVALID_CODE",
                outcome.FailureReason ?? "The verification code is incorrect.", 400);
        }

        MarkVerified(ret, ev, outcome.EvcReference);
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("E-verified return {ReturnId} via {Mode} ref={Ref}", returnId, ev.Mode, ev.EvcReference);

        return BuildStatus(ret, ev);
    }

    // =========================================================== internals

    private async Task<TaxReturn> LoadOwnedReturnAsync(Guid id, CancellationToken ct)
        => await _db.TaxReturns.FirstOrDefaultAsync(
               r => r.Id == id && r.TenantId == _currentUser.TenantId && r.UserId == _currentUser.UserId, ct)
           ?? throw AppException.NotFound("Tax return not found.", "RETURN.NOT_FOUND");

    private static void EnsureFiled(TaxReturn ret)
    {
        if (ret.Status is not (ReturnStatus.Filed or ReturnStatus.Processed) || ret.SubmittedAt is null)
        {
            throw new AppException("EVERIFY.NOT_FILED",
                "The return must be filed before it can be e-verified.", 409);
        }
    }

    private EVerification NewRow(TaxReturn ret) => new()
    {
        TenantId = ret.TenantId,
        TaxReturnId = ret.Id,
        MaxAttempts = 5,
    };

    private void MarkVerified(TaxReturn ret, EVerification ev, string? evcReference)
    {
        var now = _clock.UtcNow;
        ev.Status = EVerificationStatus.Verified;
        ev.VerifiedAt = now;
        ev.EvcReference = evcReference;
        ev.FailureReason = null;
        ret.EVerifiedAt = now;
    }

    private async Task<EVerifyContext> BuildContextAsync(TaxReturn ret, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == ret.UserId, ct);
        var profile = await _db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == ret.UserId, ct);
        var ay = await _db.AssessmentYears.FirstOrDefaultAsync(a => a.Id == ret.AssessmentYearId, ct);

        return new EVerifyContext(
            ret.Id,
            ay?.Code ?? string.Empty,
            ret.AcknowledgmentNumber ?? string.Empty,
            user?.PanMasked,
            user?.MobileE164,
            profile?.AadhaarLast4);
    }

    private EVerificationStatusDto BuildStatus(TaxReturn ret, EVerification? ev)
    {
        var isFiled = ret.Status is ReturnStatus.Filed or ReturnStatus.Processed && ret.SubmittedAt is not null;
        var isVerified = ret.EVerifiedAt is not null;

        DateOnly? verifyBy = null;
        int? daysRemaining = null;
        var overdue = false;
        if (ret.SubmittedAt is { } filed)
        {
            // The 30-day window runs from the filing date in IST (the date the ITD records).
            var filedIst = filed.ToOffset(TimeSpan.FromHours(5.5));
            verifyBy = DateOnly.FromDateTime(filedIst.Date).AddDays(VerificationWindowDays);
            daysRemaining = verifyBy.Value.DayNumber - _clock.TodayIst.DayNumber;
            overdue = !isVerified && daysRemaining < 0;
        }

        return new EVerificationStatusDto(
            ret.Id,
            isFiled,
            isVerified,
            ev?.Mode,
            ev?.Status,
            ev?.TransactionId,
            ev?.ChallengeExpiresAt,
            ev?.EvcReference,
            ret.SubmittedAt,
            ret.EVerifiedAt,
            verifyBy,
            daysRemaining,
            overdue,
            isFiled && !isVerified ? AllModes : NoModes,
            ev?.FailureReason);
    }
}
