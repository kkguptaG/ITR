namespace TallyG.Tax.Domain.Enums;

/// <summary>
/// Post-processing income-tax refund/demand state of a return, mirroring the ITD "Know Your Refund
/// Status" / CPC s.143(1) outcomes. A refund is only determined after the return is processed; for a
/// refund-due return it then moves Determined → SentToBank → Paid (or Failed → re-issue), while a
/// payable return yields a demand and a nil return yields neither.
/// </summary>
public enum RefundStatus
{
    /// <summary>The return has not yet been processed by CPC — no refund/demand determined.</summary>
    NotDetermined = 0,

    /// <summary>Processed with neither a refund nor a demand (return accepted as filed).</summary>
    NoRefundOrDemand = 1,

    /// <summary>A refund has been determined in the s.143(1) intimation, awaiting disbursal.</summary>
    RefundDetermined = 2,

    /// <summary>The refund has been forwarded to the refund banker (SBI) for credit.</summary>
    RefundSentToBank = 3,

    /// <summary>The refund has been credited to the pre-validated bank account.</summary>
    RefundPaid = 4,

    /// <summary>The bank credit failed (invalid/closed account) — a re-issue is required.</summary>
    RefundFailed = 5,

    /// <summary>The refund was set off against an outstanding demand u/s 245.</summary>
    RefundAdjusted = 6,

    /// <summary>A tax demand was determined in the intimation — there is no refund.</summary>
    DemandDetermined = 7
}
