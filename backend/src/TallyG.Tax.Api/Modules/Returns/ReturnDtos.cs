// Returns/Filing module — request/response DTOs (docs 04 §4.2 Tax Returns).
// JSON is camelCase on the wire (ASP.NET Core default), mapping to these PascalCase records.
// Money is decimal (NUMERIC(14,2)); the API serializes it per the foundation's JSON options.

using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Api.Modules.Returns;

// ----------------------------------------------------------------- return header

/// <summary>
/// POST /returns body. Provide the assessment-year code (e.g. "AY2025-26"). The ITR type is
/// optional — when omitted the auto-selector classifies it later (POST /returns/{id}:suggest-type).
/// </summary>
public sealed record CreateReturnRequest(string AssessmentYear, ItrType? ItrType, Regime? Regime);

/// <summary>PATCH /returns/{id} body. Only the supplied (non-null) fields are applied.</summary>
public sealed record UpdateReturnRequest(
    ItrType? ItrType,
    Regime? Regime,
    string? AnswersJson,
    decimal? TdsPaid = null,
    decimal? TcsPaid = null,
    decimal? AdvanceTaxPaid = null,
    decimal? SelfAssessmentTaxPaid = null,
    decimal? BroughtForwardHousePropertyLoss = null,
    decimal? BroughtForwardBusinessLoss = null,
    decimal? BroughtForwardShortTermCapitalLoss = null,
    decimal? BroughtForwardLongTermCapitalLoss = null,
    decimal? BroughtForwardAmtCredit = null,
    decimal? Relief89 = null,
    decimal? ForeignIncomeDoublyTaxed = null,
    decimal? ForeignTaxPaid = null,
    bool? ForeignDtaaApplies = null);

/// <summary>List-row projection for GET /returns.</summary>
public sealed record ReturnSummaryDto(
    Guid Id,
    string AssessmentYear,
    ItrType? ItrType,
    ReturnStatus Status,
    Regime? Regime,
    string? AcknowledgmentNumber,
    DateTimeOffset CreatedAt,
    DateTimeOffset? SubmittedAt);

/// <summary>
/// Full return detail for GET /returns/{id}: header plus every income head, the deductions,
/// and the latest persisted computation (if any).
/// </summary>
public sealed record ReturnDetailDto(
    Guid Id,
    string AssessmentYear,
    ItrType? ItrType,
    ReturnStatus Status,
    Regime? Regime,
    string RuleSetVersion,
    string QuestionnaireSchemaVersion,
    string AnswersJson,
    string FilingMode,
    bool IsRevised,
    string? AcknowledgmentNumber,
    DateTimeOffset CreatedAt,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset? EVerifiedAt,
    IReadOnlyList<IncomeSourceDto> IncomeSources,
    IReadOnlyList<SalaryDetailDto> Salaries,
    IReadOnlyList<HousePropertyDto> HouseProperties,
    IReadOnlyList<CapitalGainDto> CapitalGains,
    IReadOnlyList<BusinessIncomeDto> BusinessIncomes,
    IReadOnlyList<DeductionDto> Deductions,
    TaxComputationDto? LatestComputation,
    decimal TdsPaid,
    decimal TcsPaid,
    decimal AdvanceTaxPaid,
    decimal SelfAssessmentTaxPaid,
    decimal BroughtForwardHousePropertyLoss,
    decimal BroughtForwardBusinessLoss,
    decimal BroughtForwardShortTermCapitalLoss,
    decimal BroughtForwardLongTermCapitalLoss,
    decimal BroughtForwardAmtCredit,
    decimal Relief89,
    decimal ForeignIncomeDoublyTaxed,
    decimal ForeignTaxPaid,
    bool ForeignDtaaApplies);

// ----------------------------------------------------------------- income sources

/// <summary>POST/PATCH body for a generic income source (salary, house-property, CG, business, other).</summary>
public sealed record UpsertIncomeSourceRequest(IncomeType Type, string? Label, decimal Amount, string? SourceMetaJson);

public sealed record IncomeSourceDto(Guid Id, IncomeType Type, string? Label, decimal Amount, string SourceMetaJson);

// ----------------------------------------------------------------- salary

public sealed record UpsertSalaryRequest(
    string Employer,
    string? Tan,
    decimal Gross,
    decimal Hra,
    decimal Perquisites,
    decimal ProfitsInLieu,
    decimal ExemptAllowances,
    decimal HraExemption,
    decimal StdDeduction,
    decimal ProfessionalTax)
{
    /// <summary>Optional Schedule S breakup; when present it rolls up into the fields above.
    /// A settable init property (not a positional param) so System.Text.Json binds the JSON array.</summary>
    public IReadOnlyList<UpsertSalaryComponentRequest>? Components { get; init; }
}

/// <summary>One row of the Schedule S salary breakup grid (Particular / Type / Total / Exempt).</summary>
public sealed record UpsertSalaryComponentRequest(
    string Label,
    SalaryComponentCategory Category,
    decimal Total,
    decimal Exempt,
    bool IsHra);

public sealed record SalaryDetailDto(
    Guid Id,
    string Employer,
    string? Tan,
    decimal Gross,
    decimal Hra,
    decimal Perquisites,
    decimal ProfitsInLieu,
    decimal ExemptAllowances,
    decimal HraExemption,
    decimal StdDeduction,
    decimal ProfessionalTax,
    IReadOnlyList<SalaryComponentDto> Components);

public sealed record SalaryComponentDto(
    Guid Id,
    string Label,
    SalaryComponentCategory Category,
    decimal Total,
    decimal Exempt,
    decimal Taxable,
    bool IsHra);

// ----------------------------------------------------------------- house property

public sealed record UpsertHousePropertyRequest(
    HousePropertyType Type,
    string? Address,
    decimal AnnualValue,
    decimal AnnualRent,
    decimal MunicipalTaxPaid,
    decimal InterestOnLoan,
    decimal CoOwnerSharePct);

public sealed record HousePropertyDto(
    Guid Id,
    HousePropertyType Type,
    string? Address,
    decimal AnnualValue,
    decimal AnnualRent,
    decimal MunicipalTaxPaid,
    decimal StdDeduction30Pct,
    decimal InterestOnLoan,
    decimal CoOwnerSharePct,
    decimal NetIncome);

// ----------------------------------------------------------------- capital gains

public sealed record UpsertCapitalGainRequest(
    CapitalGainAssetType AssetType,
    CapitalGainTerm Term,
    string? TaxSection,
    DateOnly? AcquisitionDate,
    DateOnly? TransferDate,
    decimal SalePrice,
    decimal CostOfAcquisition,
    decimal CostOfImprovement,
    decimal ExpensesOnTransfer,
    string? ExemptionSection,
    decimal ExemptionAmount,
    decimal ReinvestmentAmount,
    string? Isin,
    decimal FairMarketValue31Jan2018 = 0m);

public sealed record CapitalGainDto(
    Guid Id,
    CapitalGainAssetType AssetType,
    CapitalGainTerm Term,
    string? TaxSection,
    DateOnly? AcquisitionDate,
    DateOnly? TransferDate,
    decimal SalePrice,
    decimal CostOfAcquisition,
    decimal IndexedCost,
    decimal CostOfImprovement,
    decimal ExpensesOnTransfer,
    string? ExemptionSection,
    decimal ExemptionAmount,
    decimal ReinvestmentAmount,
    decimal Gain,
    string? Isin,
    decimal FairMarketValue31Jan2018);

// ----------------------------------------------------------------- immovable-property buyers (s.194-IA)

public sealed record UpsertCapitalGainBuyerRequest(
    string BuyerName,
    string? BuyerPan,
    string? BuyerAadhaar,
    decimal PercentageShare,
    decimal Amount,
    string AddressOfProperty,
    string StateCode,
    int PinCode);

public sealed record CapitalGainBuyerDto(
    Guid Id,
    Guid CapitalGainId,
    string BuyerName,
    string? BuyerPan,
    string? BuyerAadhaar,
    decimal PercentageShare,
    decimal Amount,
    string AddressOfProperty,
    string StateCode,
    int PinCode);

// ----------------------------------------------------------------- business income

public sealed record UpsertBusinessIncomeRequest(
    string? NatureOfBusinessCode,
    string? AccountingMethod,
    bool IsPresumptive,
    string? PresumptiveSection,
    decimal Turnover,
    decimal GrossReceiptsDigital,
    decimal GrossReceiptsCash,
    decimal NetProfit,
    bool SpeculativeFlag,
    decimal GstTurnoverReported);

public sealed record BusinessIncomeDto(
    Guid Id,
    string? NatureOfBusinessCode,
    string AccountingMethod,
    bool IsPresumptive,
    string? PresumptiveSection,
    decimal Turnover,
    decimal GrossReceiptsDigital,
    decimal GrossReceiptsCash,
    decimal PresumptiveRatePct,
    decimal NetProfit,
    bool SpeculativeFlag,
    decimal GstTurnoverReported);

// ----------------------------------------------------------------- deductions

public sealed record UpsertDeductionRequest(
    string Section,
    string? SubType,
    string? Description,
    decimal Amount,
    Regime? RegimeApplicable);

public sealed record DeductionDto(
    Guid Id,
    string Section,
    string? SubType,
    string? Description,
    decimal Amount,
    decimal? EligibleAmount,
    Regime? RegimeApplicable);

// ----------------------------------------------------------------- validate / submit / status

/// <summary>One completeness/business-rule finding from POST /returns/{id}:validate.</summary>
public sealed record ValidationFinding(string Severity, string Code, string Message, string? Field);

/// <summary>
/// Result of POST /returns/{id}:validate. <see cref="CanFile"/> is true only when no finding has
/// severity "block" (docs 03 §3.9 gates filing on unacknowledged block-severity items).
/// </summary>
public sealed record ValidateReturnResponse(bool CanFile, IReadOnlyList<ValidationFinding> Findings);

/// <summary>Result of POST /returns/{id}:submit — the e-filing acknowledgment + new status.</summary>
public sealed record SubmitReturnResponse(
    Guid Id,
    ReturnStatus Status,
    string AcknowledgmentNumber,
    DateTimeOffset SubmittedAt,
    int VersionNo,
    string SnapshotHash);

/// <summary>Result of GET /returns/{id}/status — the lifecycle position of the return.</summary>
public sealed record ReturnStatusDto(
    Guid Id,
    ReturnStatus Status,
    string? AcknowledgmentNumber,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset? EVerifiedAt);

// ----------------------------------------------------------------- computation

/// <summary>A persisted (or freshly computed) tax computation for one regime.</summary>
public sealed record TaxComputationDto(
    Guid Id,
    Regime Regime,
    decimal GrossTotalIncome,
    decimal TotalDeductions,
    decimal TaxableIncome,
    decimal TaxBeforeCess,
    decimal Cess,
    decimal Rebate87A,
    decimal Surcharge,
    decimal TotalTax,
    decimal TdsPaid,
    decimal AdvanceTax,
    decimal InterestPenalty,
    decimal Interest234A,
    decimal Interest234B,
    decimal Interest234C,
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
    decimal UnabsorbedDepreciationCarriedForward,
    bool IsRecommended,
    DateTimeOffset ComputedAt);
