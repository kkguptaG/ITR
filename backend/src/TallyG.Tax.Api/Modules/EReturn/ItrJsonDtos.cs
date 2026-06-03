// EReturn module — offline-filing ITR JSON: generate → validate → save to a list → download,
// for manual upload on the Income Tax e-filing portal (pre-ERI model). camelCase on the wire.

using TallyG.Tax.Api.Modules.Accounting;
using TallyG.Tax.Domain.Entities;
using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Api.Modules.EReturn;

/// <summary>One validation finding. Severity is "error" (blocks filing) or "warning". Every finding
/// carries a concrete <see cref="Suggestion"/> for how to resolve it.</summary>
public sealed record ValidationIssueDto(string Severity, string Code, string Path, string Message, string Suggestion);

/// <summary>Result of validating a generated ITR JSON before portal upload.</summary>
public sealed record ValidationReportDto(
    bool IsValid,
    int ErrorCount,
    int WarningCount,
    IReadOnlyList<ValidationIssueDto> Issues,
    string Notice);

/// <summary>A saved ITR JSON artifact in the "ready to file" list (metadata only).</summary>
public sealed record ItrJsonArtifactDto(
    Guid Id,
    Guid ReturnId,
    string AssessmentYear,
    ItrType ItrType,
    string SchemaVersion,
    ItrFilingStatus Status,
    bool IsValid,
    int ErrorCount,
    int WarningCount,
    string FileName,
    long SizeBytes,
    string? JsonHash,
    DateTimeOffset GeneratedAt,
    DateTimeOffset? ValidatedAt);

/// <summary>POST .../itr-json:generate response — the saved artifact + its validation report.</summary>
public sealed record GenerateItrJsonResponse(ItrJsonArtifactDto Artifact, ValidationReportDto Validation);

/// <summary>Bytes to stream for a JSON download.</summary>
public sealed record ItrJsonDownload(byte[] Content, string FileName);

/// <summary>The generator's output: the JSON document + the ITD schema version + form name.</summary>
public sealed record GeneratedItrJson(string Json, string SchemaVersion, string FormName);

/// <summary>
/// Everything the generator/validator need for one return, loaded once by the orchestrator.
/// Regime-agnostic: the heads are the common capture model; the form is a mapping over it.
/// </summary>
public sealed class ItrFilingContext
{
    public required TaxReturn Return { get; init; }
    public required User User { get; init; }
    public UserProfile? Profile { get; init; }
    public AssessmentYear? Ay { get; init; }
    public TaxComputation? Computation { get; init; }

    /// <summary>JSON creation date stamped into CreationInfo.JSONCreationDate (set by the caller's clock).</summary>
    public DateOnly GeneratedOn { get; init; } = new(2026, 6, 1);

    /// <summary>Balance Sheet + P&amp;L derived from the user's books (ITR-3 PARTA_BS/PARTA_PL source); null when N/A.</summary>
    public FinancialStatementsDto? FinancialStatements { get; init; }
    public IReadOnlyList<SalaryDetail> Salaries { get; init; } = Array.Empty<SalaryDetail>();
    public IReadOnlyList<HouseProperty> Houses { get; init; } = Array.Empty<HouseProperty>();
    public IReadOnlyList<CapitalGain> Gains { get; init; } = Array.Empty<CapitalGain>();
    public IReadOnlyList<BusinessIncome> Businesses { get; init; } = Array.Empty<BusinessIncome>();
    public IReadOnlyList<IncomeSource> OtherIncomes { get; init; } = Array.Empty<IncomeSource>();
    public IReadOnlyList<Deduction> Deductions { get; init; } = Array.Empty<Deduction>();
    public IReadOnlyList<BankAccountDetail> BankAccounts { get; init; } = Array.Empty<BankAccountDetail>();

    /// <summary>Deductor-wise TDS (Schedule TDS1/TDS2 source) + self-paid challans (Schedule IT source).</summary>
    public IReadOnlyList<TdsEntry> TdsEntries { get; init; } = Array.Empty<TdsEntry>();
    public IReadOnlyList<TaxPaymentChallan> Challans { get; init; } = Array.Empty<TaxPaymentChallan>();

    /// <summary>Schedule AL declaration (movable assets + liabilities, &gt;₹50L income); null when not declared.</summary>
    public TallyG.Tax.Domain.Entities.AssetsLiabilities? AssetsLiabilities { get; init; }

    /// <summary>Immovable properties declared in Schedule AL's ImmovableDetails list.</summary>
    public IReadOnlyList<ImmovablePropertyAL> ImmovablePropertiesAL { get; init; } = Array.Empty<ImmovablePropertyAL>();

    /// <summary>Interests in a firm/AOP declared in Schedule AL's InterestHeldInaAsset list (ITR-3).</summary>
    public IReadOnlyList<FirmInterestAL> FirmInterestsAL { get; init; } = Array.Empty<FirmInterestAL>();

    /// <summary>Foreign bank accounts disclosed in Schedule FA (resident only).</summary>
    public IReadOnlyList<ForeignBankAccount> ForeignBankAccounts { get; init; } = Array.Empty<ForeignBankAccount>();

    /// <summary>Foreign custodial / brokerage accounts (Schedule FA DtlsForeignCustodialAcc).</summary>
    public IReadOnlyList<ForeignCustodialAccount> ForeignCustodialAccounts { get; init; } = Array.Empty<ForeignCustodialAccount>();

    /// <summary>Foreign equity / debt interests (Schedule FA DtlsForeignEquityDebtInterest).</summary>
    public IReadOnlyList<ForeignEquityDebtInterest> ForeignEquityDebtInterests { get; init; } = Array.Empty<ForeignEquityDebtInterest>();

    /// <summary>Immovable property held abroad (Schedule FA DetailsImmovableProperty).</summary>
    public IReadOnlyList<ForeignImmovablePropertyFA> ForeignImmovableProperties { get; init; } = Array.Empty<ForeignImmovablePropertyFA>();

    /// <summary>Financial interest in any foreign entity (Schedule FA DetailsFinancialInterest).</summary>
    public IReadOnlyList<ForeignFinancialInterest> ForeignFinancialInterests { get; init; } = Array.Empty<ForeignFinancialInterest>();

    /// <summary>Foreign accounts with signing authority (Schedule FA DetailsOfAccntsHvngSigningAuth).</summary>
    public IReadOnlyList<ForeignSigningAuthority> ForeignSigningAuthorities { get; init; } = Array.Empty<ForeignSigningAuthority>();

    /// <summary>Other income from outside India (Schedule FA DetailsOfOthSourcesIncOutsideIndia).</summary>
    public IReadOnlyList<ForeignOtherIncome> ForeignOtherIncomes { get; init; } = Array.Empty<ForeignOtherIncome>();

    /// <summary>Foreign cash-value insurance contracts (Schedule FA DtlsForeignCashValueInsurance).</summary>
    public IReadOnlyList<ForeignCashValueInsurance> ForeignCashValueInsurances { get; init; } = Array.Empty<ForeignCashValueInsurance>();

    /// <summary>Other foreign capital assets (Schedule FA DetailsOthAssets).</summary>
    public IReadOnlyList<ForeignOtherAsset> ForeignOtherAssets { get; init; } = Array.Empty<ForeignOtherAsset>();

    /// <summary>Interests in trusts outside India (Schedule FA DetailsOfTrustOutIndiaTrustee).</summary>
    public IReadOnlyList<ForeignTrustInterest> ForeignTrustInterests { get; init; } = Array.Empty<ForeignTrustInterest>();

    /// <summary>Itemised 80G donations (Schedule 80G donee-wise tables); empty falls back to totals-only.</summary>
    public IReadOnlyList<Donation80G> Donations80G { get; init; } = Array.Empty<Donation80G>();

    /// <summary>Exempt-income items disclosed in Schedule EI (ITR-2/3).</summary>
    public IReadOnlyList<ExemptIncome> ExemptIncomes { get; init; } = Array.Empty<ExemptIncome>();

    /// <summary>Foreign-source income + tax-relief items disclosed in Schedule FSI / TR1 (ITR-2/3).</summary>
    public IReadOnlyList<ForeignSourceIncome> ForeignSourceIncomes { get; init; } = Array.Empty<ForeignSourceIncome>();

    /// <summary>Clubbed income of specified persons disclosed in Schedule SPI (ITR-2/3).</summary>
    public IReadOnlyList<ClubbedIncome> ClubbedIncomes { get; init; } = Array.Empty<ClubbedIncome>();

    /// <summary>Pass-through income (business trust / investment fund) disclosed in Schedule PTI (ITR-2/3).</summary>
    public IReadOnlyList<PassThroughIncome> PassThroughIncomes { get; init; } = Array.Empty<PassThroughIncome>();

    /// <summary>Collector-wise TCS rows (tax collected at source) disclosed in Schedule TCS.</summary>
    public IReadOnlyList<TcsEntry> TcsEntries { get; init; } = Array.Empty<TcsEntry>();

    /// <summary>Portuguese-Civil-Code spouse apportionment (Schedule 5A); null unless declared.</summary>
    public SpouseIncomeApportionment? SpouseApportionment { get; init; }

    public string AyCode => Ay?.Code ?? Return.RuleSetVersion;
    public ItrType ItrType => Return.ItrType ?? TallyG.Tax.Domain.Enums.ItrType.ITR1;
}
