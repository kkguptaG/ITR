using Microsoft.EntityFrameworkCore;
using TallyG.Tax.Api.Modules.Returns;
using TallyG.Tax.Domain.Abstractions;
using TallyG.Tax.Domain.Common;
using TallyG.Tax.Domain.Entities;
using TallyG.Tax.Domain.Enums;
using TallyG.Tax.Infrastructure.Persistence;
using TallyG.Tax.Api.Common;

namespace TallyG.Tax.Api.Modules.Refunds;

/// <summary>
/// Income-tax refund/demand tracking (docs 04). A refund is only determined after CPC processes the
/// return (s.143(1)). On read this first reconciles the return's processing status (reusing
/// <see cref="IReturnService.GetStatusAsync"/>, which advances Filed → Processed once e-verified),
/// then reconciles the refund itself with the ITD via <see cref="IRefundStatusClient"/> — advancing a
/// refund-due return determined → sent-to-bank → paid, settling a payable return to a demand, and a
/// nil return to neither. Owner-scoped; auto-registered by Scrutor.
/// </summary>
public sealed class RefundService : IRefundService
{
    /// <summary>Once a return reaches one of these, the ITD has nothing more to report (until a re-issue).</summary>
    private static readonly HashSet<RefundStatus> Final = new()
    {
        RefundStatus.RefundPaid,
        RefundStatus.RefundFailed,
        RefundStatus.RefundAdjusted,
        RefundStatus.NoRefundOrDemand,
        RefundStatus.DemandDetermined,
    };

    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTime _clock;
    private readonly IRefundStatusClient _client;
    private readonly IReturnService _returns;
    private readonly ILogger<RefundService> _logger;

    public RefundService(
        AppDbContext db,
        ICurrentUser currentUser,
        IDateTime clock,
        IRefundStatusClient client,
        IReturnService returns,
        ILogger<RefundService> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _client = client;
        _returns = returns;
        _logger = logger;
    }

    // =========================================================== status

    public async Task<RefundStatusDto> GetAsync(Guid returnId, CancellationToken ct = default)
    {
        // Reconcile the return's processing status first (Filed -> Processed once e-verified). This
        // ownership-checks the return too (404 if not the caller's).
        var status = await _returns.GetStatusAsync(returnId, ct);
        var ret = await LoadOwnedReturnAsync(returnId, ct);

        if (ret.Status != ReturnStatus.Processed)
        {
            // Not processed yet — nothing for CPC to determine. Don't persist a tracking row.
            return NotDeterminedDto(returnId);
        }

        var refundOrPayable = await LatestRefundOrPayableAsync(returnId, ct);
        var tracking = await _db.RefundTrackings.FirstOrDefaultAsync(r => r.TaxReturnId == returnId, ct);

        var now = _clock.UtcNow;
        if (tracking is null)
        {
            tracking = new RefundTracking
            {
                TenantId = ret.TenantId,
                TaxReturnId = ret.Id,
                Status = RefundStatus.NotDetermined,
                IntimationDate = now, // the s.143(1) intimation date — first determination
            };
            _db.RefundTrackings.Add(tracking);
        }

        // Amounts come straight from the (signed) computed result; they don't change between polls.
        tracking.DeterminedAmount = refundOrPayable > 0m ? refundOrPayable : 0m;
        tracking.DemandAmount = refundOrPayable < 0m ? -refundOrPayable : 0m;

        if (!Final.Contains(tracking.Status))
        {
            var poll = await _client.PollAsync(
                new RefundPollContext(status.AcknowledgmentNumber ?? string.Empty, refundOrPayable, tracking.Status), ct);
            ApplyPoll(tracking, poll, now, ret.UserId);
        }

        tracking.LastPolledAt = now;
        await _db.SaveChangesAsync(ct);

        return await BuildDtoAsync(ret, tracking, ct);
    }

    // =========================================================== re-issue

    public async Task<RefundStatusDto> RequestReissueAsync(Guid returnId, CancellationToken ct = default)
    {
        var ret = await LoadOwnedReturnAsync(returnId, ct);
        var tracking = await _db.RefundTrackings.FirstOrDefaultAsync(r => r.TaxReturnId == returnId, ct)
                       ?? throw AppException.NotFound("No refund has been determined for this return yet.", "REFUND.NOT_FOUND");

        if (tracking.Status != RefundStatus.RefundFailed)
        {
            throw new AppException("REFUND.NOT_FAILED",
                "A refund re-issue can only be requested after a failed credit.", 409);
        }

        // Back to "determined" so the next poll re-runs the disbursal to the (corrected) bank account.
        tracking.Status = RefundStatus.RefundDetermined;
        tracking.FailureReason = null;
        tracking.RefundSequenceNo = null;
        tracking.PaidAt = null;
        tracking.CreditedAccountLast4 = null;
        tracking.ReissueCount += 1;
        tracking.LastPolledAt = _clock.UtcNow;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Refund re-issue requested for return {ReturnId} (attempt {N})", returnId, tracking.ReissueCount);
        return await BuildDtoAsync(ret, tracking, ct);
    }

    // =========================================================== internals

    private async Task<TaxReturn> LoadOwnedReturnAsync(Guid id, CancellationToken ct)
        => await _db.TaxReturns.FirstOrDefaultAsync(
               r => r.Id == id && r.TenantId == _currentUser.TenantId && r.UserId == _currentUser.UserId, ct)
           ?? throw AppException.NotFound("Tax return not found.", "RETURN.NOT_FOUND");

    private static RefundStatusDto NotDeterminedDto(Guid returnId) => new(
        returnId, IsProcessed: false, RefundStatus.NotDetermined,
        DeterminedAmount: 0m, DemandAmount: 0m, Mode: null, RefundSequenceNo: null,
        CreditedAccountLast4: null, RefundBankName: null, IntimationDate: null, PaidAt: null,
        FailureReason: null, ReissueCount: 0, CanReissue: false);

    /// <summary>The recommended (else newest) computation's signed refund/payable; 0 if none computed.</summary>
    private async Task<decimal> LatestRefundOrPayableAsync(Guid returnId, CancellationToken ct)
    {
        // SQLite can't ORDER BY a DateTimeOffset column, so materialize then order in memory.
        var comps = await _db.TaxComputations
            .Where(c => c.TaxReturnId == returnId)
            .Select(c => new { c.IsRecommended, c.ComputedAt, c.RefundOrPayable })
            .ToListAsync(ct);

        return comps
            .OrderByDescending(c => c.IsRecommended)
            .ThenByDescending(c => c.ComputedAt)
            .Select(c => c.RefundOrPayable)
            .FirstOrDefault();
    }

    private void ApplyPoll(RefundTracking tracking, RefundStatusResult poll, DateTimeOffset now, Guid userId)
    {
        tracking.Status = poll.Status;
        tracking.FailureReason = poll.FailureReason;

        if (poll.Status == RefundStatus.RefundPaid)
        {
            tracking.Mode = poll.Mode;
            tracking.RefundSequenceNo = poll.RefundSequenceNo;
            tracking.PaidAt ??= now;
            // The credited account is the user's pre-validated refund account.
            tracking.CreditedAccountLast4 ??= Last4(RefundAccount(userId)?.AccountNumber);
        }
    }

    private async Task<RefundStatusDto> BuildDtoAsync(TaxReturn ret, RefundTracking t, CancellationToken ct)
    {
        var account = await _db.BankAccountDetails
            .FirstOrDefaultAsync(b => b.UserId == ret.UserId && b.UseForRefund, ct);

        return new RefundStatusDto(
            ret.Id,
            IsProcessed: true,
            t.Status,
            t.DeterminedAmount,
            t.DemandAmount,
            t.Mode,
            t.RefundSequenceNo,
            t.CreditedAccountLast4 ?? Last4(account?.AccountNumber),
            account?.BankName,
            t.IntimationDate,
            t.PaidAt,
            t.FailureReason,
            t.ReissueCount,
            CanReissue: t.Status == RefundStatus.RefundFailed);
    }

    private BankAccountDetail? RefundAccount(Guid userId)
        => _db.BankAccountDetails.FirstOrDefault(b => b.UserId == userId && b.UseForRefund);

    private static string? Last4(string? accountNumber)
    {
        if (string.IsNullOrWhiteSpace(accountNumber) || accountNumber.Length < 4)
        {
            return null;
        }

        return accountNumber[^4..];
    }
}
