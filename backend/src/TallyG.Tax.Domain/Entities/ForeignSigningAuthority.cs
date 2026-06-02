using TallyG.Tax.Domain.Common;

namespace TallyG.Tax.Domain.Entities;

/// <summary>
/// A foreign account in which the resident has signing authority, disclosed in Schedule FA
/// (DetailsOfAccntsHvngSigningAuth) — e.g. a signatory on a foreign employer's / family's account.
/// Return-scoped; non-disclosure carries Black Money Act penalties.
/// </summary>
public class ForeignSigningAuthority : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid TaxReturnId { get; set; }

    public string CountryCode { get; set; } = string.Empty;
    public string CountryName { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;

    /// <summary>Name of the institution holding the account (NameOfInstitution).</summary>
    public string InstitutionName { get; set; } = string.Empty;
    public string InstitutionAddress { get; set; } = string.Empty;

    /// <summary>Name in which the account is held (NameMentionedInAccnt).</summary>
    public string AccountHolderName { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;

    public decimal PeakBalanceOrInvestment { get; set; }

    /// <summary>Whether income accrued in the account is taxable in the assessee's hands (IncAccuredTaxFlag Y/N).</summary>
    public bool IncomeTaxable { get; set; }

    /// <summary>Income accrued in the account (IncAccuredInAcc).</summary>
    public decimal IncomeAccrued { get; set; }

    /// <summary>Income offered to tax in the return (IncOfferedAmt).</summary>
    public decimal IncomeOffered { get; set; }

    /// <summary>Which schedule the income was offered in: SA / HP / CG / OS / EI / NI.</summary>
    public string IncomeTaxSchedule { get; set; } = "OS";

    /// <summary>Item / row reference within that schedule.</summary>
    public string IncomeTaxScheduleItem { get; set; } = "1";

    public TaxReturn? TaxReturn { get; set; }
}
