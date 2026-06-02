using TallyG.Tax.Domain.Common;

namespace TallyG.Tax.Domain.Entities;

/// <summary>
/// A foreign depository / bank account held by a resident, disclosed in Schedule FA (DetailsForiegnBank).
/// Return-scoped: balances are reported per assessment year. Non-disclosure carries Black Money Act
/// penalties, so this is a high-stakes compliance schedule.
/// </summary>
public class ForeignBankAccount : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid TaxReturnId { get; set; }

    /// <summary>ITD numeric country code (excluding India). e.g. "2" = USA, "44" = UK.</summary>
    public string CountryCode { get; set; } = string.Empty;
    public string CountryName { get; set; } = string.Empty;

    public string BankName { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;

    /// <summary>Foreign account number (PII — encrypt at rest in production).</summary>
    public string AccountNumber { get; set; } = string.Empty;

    /// <summary>OWNER / BENEFICIAL_OWNER / BENIFICIARY (the ITD enum spelling).</summary>
    public string OwnerStatus { get; set; } = "OWNER";

    public DateOnly? AccountOpenDate { get; set; }

    public decimal PeakBalance { get; set; }
    public decimal ClosingBalance { get; set; }
    public decimal InterestAccrued { get; set; }

    public TaxReturn? TaxReturn { get; set; }
}
