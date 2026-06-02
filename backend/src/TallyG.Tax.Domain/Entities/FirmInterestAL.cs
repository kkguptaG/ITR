using TallyG.Tax.Domain.Common;

namespace TallyG.Tax.Domain.Entities;

/// <summary>
/// The assessee's interest in the assets of a firm / AOP / BOI as a partner or member, disclosed in
/// Schedule AL's InterestHeldInaAsset list (ITR-3 only — a partner with such an interest files ITR-3).
/// Return-scoped. Mandatory alongside the rest of Schedule AL when total income exceeds ₹50 lakh.
/// </summary>
public class FirmInterestAL : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid TaxReturnId { get; set; }

    /// <summary>Name of the firm / AOP / BOI (≤50 chars in the schema).</summary>
    public string FirmName { get; set; } = string.Empty;

    /// <summary>PAN of the firm / AOP / BOI.</summary>
    public string FirmPan { get; set; } = string.Empty;

    // Address of the firm (AddressAL).
    public string FlatDoorNo { get; set; } = string.Empty;
    public string Locality { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;

    /// <summary>ITD two-digit state code ("01".."38").</summary>
    public string StateCode { get; set; } = string.Empty;
    public string Pincode { get; set; } = string.Empty;

    /// <summary>The assessee's investment in the firm / AOP (AssesseInvestment), at cost.</summary>
    public decimal Investment { get; set; }

    public TaxReturn? TaxReturn { get; set; }
}
