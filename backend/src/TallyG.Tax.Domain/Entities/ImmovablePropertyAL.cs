using TallyG.Tax.Domain.Common;

namespace TallyG.Tax.Domain.Entities;

/// <summary>
/// One immovable property (land / building) declared in Schedule AL's ImmovableDetails list. Reported at
/// cost, with a structured address. Schedule AL is mandatory in ITR-2/3 when total income exceeds ₹50 lakh.
/// Return-scoped, and (unlike the single movable-asset declaration) there can be several rows.
/// </summary>
public class ImmovablePropertyAL : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid TaxReturnId { get; set; }

    /// <summary>Short description, e.g. "Residential flat" / "Agricultural land" (≤25 chars in the schema).</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Flat / door / building number (AddressAL.ResidenceNo).</summary>
    public string FlatDoorNo { get; set; } = string.Empty;
    public string Locality { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;

    /// <summary>ITD two-digit state code ("01".."38").</summary>
    public string StateCode { get; set; } = string.Empty;
    public string Pincode { get; set; } = string.Empty;

    /// <summary>Cost of the property (Schedule AL reports assets at cost, not market value).</summary>
    public decimal Cost { get; set; }

    public TaxReturn? TaxReturn { get; set; }
}
