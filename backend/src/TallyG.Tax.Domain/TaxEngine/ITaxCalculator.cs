using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Domain.TaxEngine;

/// <summary>
/// The pure, deterministic, AY-versioned tax computation engine (Ch.3).
///
/// Contract guarantees expected of any implementation:
///  - No I/O, no clock reads, no RNG, no network. Same input + same rule-set ⇒
///    byte-identical output, forever (reproducibility for scrutiny years later).
///  - All slabs, caps, rates, thresholds come from <see cref="TaxComputationInput.RulesJson"/>
///    (the AY-scoped TaxRuleSet), never hardcoded.
///  - Every output line is explained via <see cref="ComputationResult.Trace"/>.
///
/// The concrete implementation lives in <see cref="TaxCalculator"/> and is filled in by
/// the Tax feature agent; this interface is the binding contract other modules code to.
/// </summary>
public interface ITaxCalculator
{
    /// <summary>Compute the liability for a single regime.</summary>
    ComputationResult Compute(TaxComputationInput input, Regime regime);

    /// <summary>Compute both regimes and return the comparison with a recommendation.</summary>
    RegimeComparison Compare(TaxComputationInput input);
}

/// <summary>
/// Regime-agnostic input to the engine: the internal return model plus the rule-set JSON.
/// Money is <see cref="decimal"/> throughout (exact to the paisa).
/// </summary>
public sealed record TaxComputationInput
{
    /// <summary>Assessment year code, e.g. "AY2025-26".</summary>
    public required string AssessmentYearCode { get; init; }

    /// <summary>Rule-set version that produced/should produce this computation (pin-on-file).</summary>
    public required string RuleSetVersion { get; init; }

    /// <summary>
    /// The full TaxRuleSet document (slabs, caps, surcharge bands, cess, 87A) as JSON.
    /// The engine is the interpreter; this JSON is the law.
    /// </summary>
    public required string RulesJson { get; init; }

    /// <summary>Age of the assessee at year-end (drives senior-citizen slabs/limits).</summary>
    public int Age { get; init; }

    public IReadOnlyList<SalaryInput> Salaries { get; init; } = Array.Empty<SalaryInput>();
    public IReadOnlyList<HousePropertyInput> HouseProperties { get; init; } = Array.Empty<HousePropertyInput>();
    public IReadOnlyList<CapitalGainInput> CapitalGains { get; init; } = Array.Empty<CapitalGainInput>();
    public IReadOnlyList<BusinessIncomeInput> BusinessIncomes { get; init; } = Array.Empty<BusinessIncomeInput>();
    public IReadOnlyList<OtherIncomeInput> OtherIncomes { get; init; } = Array.Empty<OtherIncomeInput>();
    public IReadOnlyList<DeductionInput> Deductions { get; init; } = Array.Empty<DeductionInput>();

    /// <summary>Taxes already paid (reduce the final liability).</summary>
    public decimal TdsPaid { get; init; }
    public decimal TcsPaid { get; init; }
    public decimal AdvanceTaxPaid { get; init; }
    public decimal SelfAssessmentTaxPaid { get; init; }

    // --- s.234A/B/C interest context (optional; interest is 0 when the dates are absent) ---

    /// <summary>s.139(1) due date for furnishing the return (drives 234A late-filing interest).</summary>
    public DateOnly? FilingDueDate { get; init; }

    /// <summary>Date the return is/will be furnished (the "as of" date for a draft). Ends the 234A/234B periods.</summary>
    public DateOnly? ActualFilingDate { get; init; }

    /// <summary>Previous-year (FY) start, 1 Apr — places the four advance-tax installment due dates (234C).</summary>
    public DateOnly? PreviousYearStart { get; init; }

    /// <summary>Previous-year (FY) end, 31 Mar — the AY begins the next day, when 234B interest starts.</summary>
    public DateOnly? PreviousYearEnd { get; init; }

    /// <summary>True for 44AD/44ADA presumptive: a single 15-Mar advance-tax installment (234C).</summary>
    public bool PresumptiveAdvanceTax { get; init; }

    /// <summary>Quarterly advance-tax payments with dates, for exact s.234C. Empty ⇒ none assumed paid on time.</summary>
    public IReadOnlyList<AdvanceTaxInstallmentInput> AdvanceTaxInstallments { get; init; } = Array.Empty<AdvanceTaxInstallmentInput>();

    // --- Brought-forward (earlier-year) losses; each sets off ONLY against the same head's current income ---

    /// <summary>Brought-forward house-property loss (sets off only against current-year HP income; 8-year c/f).</summary>
    public decimal BroughtForwardHousePropertyLoss { get; init; }

    /// <summary>Brought-forward non-speculative business loss (sets off only against current-year business income; 8-year c/f).</summary>
    public decimal BroughtForwardBusinessLoss { get; init; }

    /// <summary>Brought-forward short-term capital loss (sets off vs current STCG and LTCG; 8-year c/f).</summary>
    public decimal BroughtForwardShortTermCapitalLoss { get; init; }

    /// <summary>Brought-forward long-term capital loss (sets off ONLY vs current LTCG; 8-year c/f).</summary>
    public decimal BroughtForwardLongTermCapitalLoss { get; init; }

    /// <summary>
    /// Brought-forward unabsorbed depreciation / allowance (s.32(2)). Unlike a business loss it sets off
    /// against income under ANY head except salary, and carries forward INDEFINITELY. Set off after the
    /// current-year inter-head set-off and the brought-forward business loss.
    /// </summary>
    public decimal BroughtForwardUnabsorbedDepreciation { get; init; }

    /// <summary>
    /// Book-vs-tax depreciation adjustment to business income (Schedule BP): book depreciation debited to the
    /// P&amp;L is added back and the s.32 (Income-tax Act) depreciation is allowed instead, so this equals
    /// (book depreciation − tax depreciation). Positive raises taxable business income, negative lowers it;
    /// nil when books and tax depreciation match. Folded into the business head before set-off.
    /// </summary>
    public decimal BusinessDepreciationAdjustment { get; init; }

    // --- Alternate Minimum Tax (s.115JC/JD) + reliefs (s.89/90/91) ---

    /// <summary>Brought-forward AMT credit u/s 115JD; set off in a year where regular tax exceeds AMT.</summary>
    public decimal BroughtForwardAmtCredit { get; init; }

    /// <summary>Relief u/s 89(1) for salary arrears (Form 10E), pre-computed (see <see cref="Section89Calculator"/>); subtracted from tax.</summary>
    public decimal Relief89 { get; init; }

    /// <summary>Foreign income that is doubly taxed (India + abroad), eligible for FTC u/s 90/90A/91.</summary>
    public decimal ForeignIncomeDoublyTaxed { get; init; }

    /// <summary>Foreign tax actually paid on that income (the credit ceiling).</summary>
    public decimal ForeignTaxPaid { get; init; }

    /// <summary>True ⇒ a DTAA exists with the source country (relief u/s 90/90A); false ⇒ unilateral relief u/s 91.</summary>
    public bool ForeignDtaaApplies { get; init; }
}

/// <summary>One advance-tax payment (amount + date paid) used for exact s.234C deferment interest.</summary>
public sealed record AdvanceTaxInstallmentInput(DateOnly PaidOn, decimal Amount);

public sealed record SalaryInput(
    string Employer,
    decimal Gross,
    decimal Perquisites,
    decimal ExemptAllowances,
    decimal HraExemption,
    decimal ProfessionalTax);

public sealed record HousePropertyInput(
    HousePropertyType Type,
    decimal AnnualValue,
    decimal MunicipalTaxesPaid,
    decimal InterestOnLoan);

public sealed record CapitalGainInput(
    CapitalGainAssetType AssetType,
    CapitalGainTerm Term,
    string? TaxSection,
    decimal SaleConsideration,
    decimal CostOfAcquisition,
    decimal CostOfImprovement,
    decimal ExpensesOnTransfer,
    decimal ExemptionAmount,
    DateOnly? AcquisitionDate,
    DateOnly? TransferDate,
    /// <summary>FMV on the 112A grandfathering date (31-Jan-2018) for pre-2018 listed equity.</summary>
    decimal? FairMarketValueOnGrandfatherDate = null,
    /// <summary>Indexed cost of acquisition for property eligible for the 20%-with-indexation option.</summary>
    decimal? IndexedCost = null,
    /// <summary>Reinvestment-exemption section for a LONG-term gain: "54" / "54F" / "54EC" (null ⇒ use the manual <see cref="ExemptionAmount"/>).</summary>
    string? ExemptionSection = null,
    /// <summary>Amount reinvested (new house u/s 54/54F, or specified bonds u/s 54EC) driving the computed exemption.</summary>
    decimal ReinvestmentAmount = 0m);

public sealed record BusinessIncomeInput(
    bool IsPresumptive,
    string? PresumptiveSection,
    decimal Turnover,
    decimal DigitalReceipts,
    decimal CashReceipts,
    decimal NetProfit,
    bool Speculative);

/// <summary>
/// Income from other sources. <paramref name="Nature"/> routes the tax treatment:
/// "lottery_115bb" → flat s.115BB rate; "agricultural" → exempt but aggregated for rate
/// (partial integration); anything else (null/"normal"/"interest"/"dividend") → slab rate.
/// </summary>
public sealed record OtherIncomeInput(string Label, decimal Amount, string? Nature = null);

public sealed record DeductionInput(string Section, decimal ClaimedAmount, string? SubType = null);

/// <summary>
/// Result of a single-regime computation. Mirrors the columns of TaxComputation (Ch.2)
/// plus the explainability trace. All amounts are decimal INR.
/// </summary>
public sealed record ComputationResult
{
    public required Regime Regime { get; init; }
    public decimal GrossTotalIncome { get; init; }
    public decimal TotalDeductions { get; init; }

    /// <summary>Taxable income, rounded to the nearest ₹10 (s.288A).</summary>
    public decimal TaxableIncome { get; init; }

    public decimal TaxBeforeRebate { get; init; }
    public decimal Rebate87A { get; init; }
    public decimal Surcharge { get; init; }
    public decimal Cess { get; init; }

    /// <summary>Total tax liability after rebate, surcharge and cess.</summary>
    public decimal TotalTax { get; init; }

    /// <summary>Combined TDS+TCS credit used in the refund/payable math.</summary>
    public decimal TdsPaid { get; init; }
    /// <summary>TCS component of <see cref="TdsPaid"/> — shown separately in the summary.</summary>
    public decimal TcsPaid { get; init; }
    /// <summary>Combined advance tax + self-assessment tax paid.</summary>
    public decimal AdvanceTax { get; init; }
    /// <summary>Self-assessment-tax component of <see cref="AdvanceTax"/> — shown separately.</summary>
    public decimal SelfAssessmentTaxPaid { get; init; }
    public decimal InterestPenalty { get; init; }

    /// <summary>Per-section split of <see cref="InterestPenalty"/>: interest u/s 234A / 234B / 234C.</summary>
    public decimal Interest234A { get; init; }
    public decimal Interest234B { get; init; }
    public decimal Interest234C { get; init; }

    /// <summary>Positive ⇒ refund due; negative ⇒ payable.</summary>
    public decimal RefundOrPayable { get; init; }

    // --- AMT (s.115JC/JD) + reliefs (s.89/90/91); all zero when not applicable ---

    /// <summary>Adjusted Total Income for AMT (total income + Part-C/10AA/35AD add-backs). 0 when AMT N/A.</summary>
    public decimal AdjustedTotalIncome { get; init; }

    /// <summary>Alternate Minimum Tax u/s 115JC (incl. its surcharge + cess). 0 when AMT N/A.</summary>
    public decimal AlternativeMinimumTax { get; init; }

    /// <summary>AMT credit generated this year and carried forward u/s 115JD (AMT − regular tax, when AMT is higher).</summary>
    public decimal AmtCreditGenerated { get; init; }

    /// <summary>Brought-forward AMT credit set off this year u/s 115JD (when regular tax exceeds AMT).</summary>
    public decimal AmtCreditSetOff { get; init; }

    /// <summary>Relief u/s 89(1) for salary arrears (Form 10E) applied against the tax.</summary>
    public decimal Relief89 { get; init; }

    /// <summary>Relief u/s 90/90A/91 — credit for foreign tax on doubly-taxed income.</summary>
    public decimal Relief90And91 { get; init; }

    // --- Current-year losses carried forward after inter-head set-off (s.71); feed next year's b/f. ---

    /// <summary>Current-year house-property loss carried forward u/s 71B (8 years, vs HP income). 0 if none.</summary>
    public decimal HousePropertyLossCarriedForward { get; init; }

    /// <summary>Current-year non-speculative business loss carried forward u/s 72 (8 years, vs business income). 0 if none.</summary>
    public decimal BusinessLossCarriedForward { get; init; }

    /// <summary>Current-year speculative business loss carried forward u/s 73 (4 years, vs speculative income). 0 if none.</summary>
    public decimal SpeculativeLossCarriedForward { get; init; }

    /// <summary>Current-year short-term capital loss carried forward u/s 74 (8 years, vs STCG/LTCG). 0 if none.</summary>
    public decimal ShortTermCapitalLossCarriedForward { get; init; }

    /// <summary>Current-year long-term capital loss carried forward u/s 74 (8 years, vs LTCG only). 0 if none.</summary>
    public decimal LongTermCapitalLossCarriedForward { get; init; }

    /// <summary>Brought-forward unabsorbed depreciation (s.32(2)) NOT set off this year — carried forward
    /// indefinitely. 0 when none b/f or fully absorbed.</summary>
    public decimal UnabsorbedDepreciationCarriedForward { get; init; }

    /// <summary>Line-by-line explanation of how each figure was derived.</summary>
    public IReadOnlyList<TraceLine> Trace { get; init; } = Array.Empty<TraceLine>();
}

/// <summary>One explainable step in the computation pipeline.</summary>
public sealed record TraceLine(string Step, string Description, decimal Amount, string? RuleRef = null);

/// <summary>Old-vs-new comparison plus the engine's recommendation.</summary>
public sealed record RegimeComparison(
    ComputationResult Old,
    ComputationResult New,
    Regime Recommended,
    decimal SavingsVsAlternative,
    string Reason);
