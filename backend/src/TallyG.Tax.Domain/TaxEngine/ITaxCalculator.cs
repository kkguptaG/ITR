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
}

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
    decimal? IndexedCost = null);

public sealed record BusinessIncomeInput(
    bool IsPresumptive,
    string? PresumptiveSection,
    decimal Turnover,
    decimal DigitalReceipts,
    decimal CashReceipts,
    decimal NetProfit,
    bool Speculative);

public sealed record OtherIncomeInput(string Label, decimal Amount);

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

    public decimal TdsPaid { get; init; }
    public decimal AdvanceTax { get; init; }
    public decimal InterestPenalty { get; init; }

    /// <summary>Positive ⇒ refund due; negative ⇒ payable.</summary>
    public decimal RefundOrPayable { get; init; }

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
