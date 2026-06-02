using TallyG.Tax.Domain.Common;

namespace TallyG.Tax.Domain.Entities;

/// <summary>
/// A foreign cash-value insurance or annuity contract, disclosed in Schedule FA
/// (DtlsForeignCashValueInsurance). Return-scoped; non-disclosure carries Black Money Act penalties.
/// </summary>
public class ForeignCashValueInsurance : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid TaxReturnId { get; set; }

    public string CountryCode { get; set; } = string.Empty;
    public string CountryName { get; set; } = string.Empty;

    /// <summary>Insurer / financial institution name (FinancialInstName).</summary>
    public string InstitutionName { get; set; } = string.Empty;
    public string InstitutionAddress { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;

    public DateOnly? ContractDate { get; set; }

    /// <summary>Cash value or surrender value of the contract (CashValOrSurrenderVal).</summary>
    public decimal CashOrSurrenderValue { get; set; }

    /// <summary>Total gross amount paid / credited during the period (TotGrossAmtPaidCredited).</summary>
    public decimal GrossAmountCredited { get; set; }

    public TaxReturn? TaxReturn { get; set; }
}
