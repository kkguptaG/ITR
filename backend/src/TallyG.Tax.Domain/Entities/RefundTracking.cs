using TallyG.Tax.Domain.Common;
using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Domain.Entities;

/// <summary>
/// Tracks the income-tax refund (or demand) of a processed <see cref="TaxReturn"/> — one row per
/// return. After CPC processes the return (s.143(1)), this records the determined refund/demand, the
/// disbursal progress to the pre-validated bank account, and any failure/re-issue. Distinct from a
/// filing-fee (Razorpay) refund, which lives on <see cref="Payment"/>.
/// </summary>
public class RefundTracking : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid TaxReturnId { get; set; }

    public RefundStatus Status { get; set; } = RefundStatus.NotDetermined;

    /// <summary>Refund determined in the intimation (≥ 0).</summary>
    public decimal DeterminedAmount { get; set; }

    /// <summary>Demand determined in the intimation (≥ 0), when payable / adjusted u/s 245.</summary>
    public decimal DemandAmount { get; set; }

    /// <summary>Disbursal mode — "ECS" (direct credit) or "Cheque".</summary>
    public string? Mode { get; set; }

    /// <summary>ITD refund sequence / reference number (the "RRN" shown on the portal).</summary>
    public string? RefundSequenceNo { get; set; }

    /// <summary>Last 4 digits of the bank account the refund was credited to.</summary>
    public string? CreditedAccountLast4 { get; set; }

    /// <summary>Date of the s.143(1) intimation that determined the refund/demand.</summary>
    public DateTimeOffset? IntimationDate { get; set; }

    /// <summary>When the refund was credited.</summary>
    public DateTimeOffset? PaidAt { get; set; }

    /// <summary>Why a credit failed (invalid account, account closed, …).</summary>
    public string? FailureReason { get; set; }

    /// <summary>How many times the assessee has requested a refund re-issue after a failure.</summary>
    public int ReissueCount { get; set; }

    /// <summary>When the status was last reconciled with the ITD.</summary>
    public DateTimeOffset? LastPolledAt { get; set; }

    public TaxReturn? TaxReturn { get; set; }
}
