// Tax (computation) module — request/response DTOs (docs 03 §3.4–3.8).
// JSON is camelCase on the wire (ASP.NET Core default). Money is decimal (NUMERIC(14,2)).
// These DTOs mirror the engine's ComputationResult / RegimeComparison so the frontend renders
// directly from the line-by-line trace, never re-deriving figures.

using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Api.Modules.Tax;

// ----------------------------------------------------------------- /tax/compute

/// <summary>POST /tax/compute body: the return to compute and (optionally) which regime.</summary>
/// <remarks>When <see cref="Regime"/> is null both regimes are computed and the cheaper one flagged.</remarks>
public sealed record ComputeRequest(Guid ReturnId, Regime? Regime);

/// <summary>Result of POST /tax/compute: persisted computation(s) + the recommended regime.</summary>
public sealed record ComputeResponse(
    Guid ReturnId,
    string AssessmentYear,
    string RuleSetVersion,
    Regime RecommendedRegime,
    decimal SavingsVsAlternative,
    string Reason,
    TaxComputationResultDto Old,
    TaxComputationResultDto New,
    bool Provisional,
    string ValidationStatus,
    string Framework,
    string? Disclaimer);

// ----------------------------------------------------------------- /tax/regime-compare & /tax/calculator

/// <summary>
/// Ad-hoc calculator / regime-compare input (no persistence). Mirrors the engine's internal return
/// model so the public calculator works for an anonymous what-if as well as for a saved return.
/// </summary>
public sealed record TaxCalculatorRequest(
    string? AssessmentYear,
    int Age,
    IReadOnlyList<SalaryInputDto>? Salaries,
    IReadOnlyList<HousePropertyInputDto>? HouseProperties,
    IReadOnlyList<CapitalGainInputDto>? CapitalGains,
    IReadOnlyList<BusinessIncomeInputDto>? BusinessIncomes,
    IReadOnlyList<OtherIncomeInputDto>? OtherIncomes,
    IReadOnlyList<DeductionInputDto>? Deductions,
    decimal TdsPaid,
    decimal TcsPaid,
    decimal AdvanceTaxPaid,
    decimal SelfAssessmentTaxPaid,
    Regime? Regime,
    decimal BroughtForwardHousePropertyLoss = 0m,
    decimal BroughtForwardBusinessLoss = 0m,
    decimal BroughtForwardShortTermCapitalLoss = 0m,
    decimal BroughtForwardLongTermCapitalLoss = 0m,
    decimal BroughtForwardAmtCredit = 0m,
    decimal Relief89 = 0m,
    decimal ForeignIncomeDoublyTaxed = 0m,
    decimal ForeignTaxPaid = 0m,
    bool ForeignDtaaApplies = false);

public sealed record SalaryInputDto(
    string? Employer,
    decimal Gross,
    decimal Perquisites,
    decimal ExemptAllowances,
    decimal HraExemption,
    decimal ProfessionalTax);

public sealed record HousePropertyInputDto(
    HousePropertyType Type,
    decimal AnnualValue,
    decimal MunicipalTaxesPaid,
    decimal InterestOnLoan);

public sealed record CapitalGainInputDto(
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
    decimal? FairMarketValueOnGrandfatherDate,
    decimal? IndexedCost,
    string? ExemptionSection = null,
    decimal ReinvestmentAmount = 0m);

public sealed record BusinessIncomeInputDto(
    bool IsPresumptive,
    string? PresumptiveSection,
    decimal Turnover,
    decimal DigitalReceipts,
    decimal CashReceipts,
    decimal NetProfit,
    bool Speculative);

public sealed record OtherIncomeInputDto(string Label, decimal Amount, string? Nature = null);

public sealed record DeductionInputDto(string Section, decimal ClaimedAmount, string? SubType);

// ----------------------------------------------------------------- results

/// <summary>A single-regime computation result with its explainability trace.</summary>
public sealed record TaxComputationResultDto(
    Regime Regime,
    decimal GrossTotalIncome,
    decimal TotalDeductions,
    decimal TaxableIncome,
    decimal TaxBeforeRebate,
    decimal Rebate87A,
    decimal Surcharge,
    decimal Cess,
    decimal TotalTax,
    decimal TdsPaid,
    decimal AdvanceTax,
    decimal InterestPenalty,
    decimal RefundOrPayable,
    decimal AdjustedTotalIncome,
    decimal AlternativeMinimumTax,
    decimal AmtCreditGenerated,
    decimal AmtCreditSetOff,
    decimal Relief89,
    decimal Relief90And91,
    decimal HousePropertyLossCarriedForward,
    decimal BusinessLossCarriedForward,
    decimal SpeculativeLossCarriedForward,
    decimal ShortTermCapitalLossCarriedForward,
    decimal LongTermCapitalLossCarriedForward,
    IReadOnlyList<TraceLineDto> Trace);

/// <summary>One explainable line of the computation pipeline.</summary>
public sealed record TraceLineDto(string Step, string Description, decimal Amount, string? RuleRef);

/// <summary>Old-vs-new comparison + the recommended regime and signed delta.</summary>
public sealed record RegimeComparisonDto(
    Regime Recommended,
    decimal SavingsVsAlternative,
    string Reason,
    TaxComputationResultDto Old,
    TaxComputationResultDto New,
    bool Provisional,
    string ValidationStatus,
    string Framework,
    string? Disclaimer);

// ----------------------------------------------------------------- /tax/slabs

/// <summary>GET /tax/slabs?ay= response: both regimes' slabs/limits for the AY (rendered from the rule-set).</summary>
public sealed record SlabsResponse(
    string AssessmentYear,
    string RuleSetVersion,
    decimal Cess,
    RegimeSlabsDto Old,
    RegimeSlabsDto New);

public sealed record RegimeSlabsDto(
    bool IsDefault,
    decimal StandardDeductionSalary,
    IReadOnlyList<SlabBandDto> Slabs,
    RebateDto? Rebate87A,
    IReadOnlyList<SurchargeBandDto> SurchargeBands);

public sealed record SlabBandDto(decimal? Upto, decimal Rate);

public sealed record RebateDto(decimal IncomeThreshold, decimal MaxRebate, bool MarginalRelief);

public sealed record SurchargeBandDto(decimal Above, decimal Rate);

// ----------------------------------------------------------------- /tax/recommendations

/// <summary>POST /tax/recommendations body: either a saved return or ad-hoc inputs.</summary>
public sealed record RecommendationsRequest(Guid? ReturnId, TaxCalculatorRequest? AdHoc);

/// <summary>Result of POST /tax/recommendations: the 80C/80D gap-analysis advisor output.</summary>
public sealed record RecommendationsResponse(
    decimal OldRegimeTax,
    decimal NewRegimeTax,
    bool RegimeSwitchBeatsDeductions,
    string Headline,
    IReadOnlyList<DeductionSuggestionDto> Suggestions);

public sealed record DeductionSuggestionDto(
    int Rank,
    string Section,
    string Label,
    decimal GapToInvest,
    decimal MarginalTaxSaved,
    decimal RoiPerRupee,
    int LockInYears,
    decimal Liquidity,
    string UtilityNote);
