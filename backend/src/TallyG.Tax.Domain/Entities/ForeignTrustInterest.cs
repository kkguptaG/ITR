using TallyG.Tax.Domain.Common;

namespace TallyG.Tax.Domain.Entities;

/// <summary>
/// An interest in a trust held outside India as a trustee / beneficiary / settlor, disclosed in
/// Schedule FA (DetailsOfTrustOutIndiaTrustee). Return-scoped; non-disclosure carries Black Money Act
/// penalties.
/// </summary>
public class ForeignTrustInterest : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid TaxReturnId { get; set; }

    public string CountryCode { get; set; } = string.Empty;
    public string CountryName { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;

    public string TrustName { get; set; } = string.Empty;
    public string TrustAddress { get; set; } = string.Empty;
    public string TrusteeNames { get; set; } = string.Empty;
    public string TrusteeAddresses { get; set; } = string.Empty;
    public string SettlorName { get; set; } = string.Empty;
    public string SettlorAddress { get; set; } = string.Empty;
    public string BeneficiaryNames { get; set; } = string.Empty;
    public string BeneficiaryAddresses { get; set; } = string.Empty;

    public DateOnly? DateHeld { get; set; }

    /// <summary>Whether income derived from the trust is taxable in the assessee's hands (IncDrvTaxFlag Y/N).</summary>
    public bool IncomeTaxable { get; set; }

    /// <summary>Income derived from the trust (IncDrvFromTrust).</summary>
    public decimal IncomeFromTrust { get; set; }

    /// <summary>Income offered to tax in the return (IncOfferedAmt).</summary>
    public decimal IncomeOffered { get; set; }

    /// <summary>Which schedule the income was offered in: SA / HP / CG / OS / EI / NI.</summary>
    public string IncomeTaxSchedule { get; set; } = "OS";

    /// <summary>Item / row reference within that schedule.</summary>
    public string IncomeTaxScheduleItem { get; set; } = "1";

    public TaxReturn? TaxReturn { get; set; }
}
