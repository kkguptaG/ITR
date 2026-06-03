// ITR auto-selector — request/response DTOs (docs 03 §3.2).
// The verdict mirrors the SelectionVerdict shape in the architecture doc: the recommended
// form plus blocked_forms and the minimal deciding_flags so the UI can explain the choice.

using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Api.Modules.Returns;

/// <summary>
/// The feature flags that drive the cascade (docs 03 §3.2.1). All flags default to false /
/// zero, so an empty input yields ITR-1. These are the canonical disqualifiers — each maps
/// 1:1 to a clause of an official form-eligibility notification.
/// </summary>
public sealed record ItrSelectorInput
{
    /// <summary>Total income for the year (drives the ₹50L cap on ITR-1 / ITR-4).</summary>
    public decimal TotalIncome { get; init; }

    /// <summary>Resident salaried / pensioner (the baseline ITR-1 profile).</summary>
    public bool HasSalaryOrPension { get; init; }

    /// <summary>Number of house properties owned (more than one disqualifies ITR-1/4).</summary>
    public int HousePropertyCount { get; init; }

    /// <summary>Any brought-forward / carried-forward loss (disqualifies ITR-1).</summary>
    public bool HasBroughtForwardLoss { get; init; }

    /// <summary>Any capital gains (disqualifies ITR-1 and ITR-4, unless only small LTCG-112A).</summary>
    public bool HasCapitalGains { get; init; }

    /// <summary>The capital gains consist ONLY of LTCG u/s 112A (listed equity / equity MF).</summary>
    public bool CapitalGainsOnlyLtcg112A { get; init; }

    /// <summary>Total LTCG-112A amount (rides on ITR-1/4 only when within the exemption threshold).</summary>
    public decimal Ltcg112AAmount { get; init; }

    /// <summary>Business / profession kept under regular books (forces ITR-3).</summary>
    public bool HasBusinessIncome { get; init; }

    /// <summary>Income under a presumptive scheme 44AD / 44ADA / 44AE.</summary>
    public bool HasPresumptiveIncome { get; init; }

    /// <summary>Speculative (intraday) business income — always regular books ⇒ ITR-3.</summary>
    public bool HasSpeculativeIncome { get; init; }

    /// <summary>F&amp;O / derivatives trading — non-speculative business ⇒ ITR-3 (Decision Log).</summary>
    public bool HasFnoIncome { get; init; }

    /// <summary>Foreign assets or foreign income (disqualifies ITR-1/4).</summary>
    public bool HasForeignAssetsOrIncome { get; init; }

    /// <summary>Director in a company, or holds unlisted shares (disqualifies ITR-1/4).</summary>
    public bool IsDirectorOrUnlistedShares { get; init; }

    /// <summary>RNOR or Non-resident (disqualifies ITR-1/4).</summary>
    public bool IsNonResidentOrRnor { get; init; }

    /// <summary>Agricultural income above ₹5,000 (disqualifies ITR-1/4).</summary>
    public bool HasAgriIncomeAbove5000 { get; init; }

    /// <summary>Partner in a firm drawing remuneration / interest (forces ITR-3).</summary>
    public bool IsPartnerInFirm { get; init; }

    /// <summary>Crypto / VDA income requiring Schedule VDA (disqualifies ITR-1/4).</summary>
    public bool HasCryptoVda { get; init; }

    /// <summary>Winnings from lotteries / betting (s.115BB) or online games (s.115BBJ) — special-rate
    /// income that ITR-1 (Sahaj) and ITR-4 (Sugam) cannot report, so it forces ITR-2/3.</summary>
    public bool HasWinnings { get; init; }
}

/// <summary>
/// The selector outcome. <see cref="BlockedForms"/> lists, per form that was ruled out, the
/// disqualifying flags; <see cref="DecidingFlags"/> is the minimal set that forced the final
/// choice (docs 03 §3.2.2). This lets the UI say "you sold shares — that needs ITR-2".
/// </summary>
public sealed record ItrSelectionVerdict(
    ItrType RecommendedForm,
    string Confidence,
    IReadOnlyDictionary<string, IReadOnlyList<string>> BlockedForms,
    IReadOnlyList<string> DecidingFlags,
    string Explanation);
