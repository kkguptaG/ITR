using TallyG.Tax.Domain.Common;

namespace TallyG.Tax.Domain.Entities;

/// <summary>
/// A buyer of an immovable property sold during the year (s.194-IA), linked to the immovable-property
/// <see cref="CapitalGain"/>. Reported in Schedule CG's per-property buyer table (TrnsfImmblPrprtyDtls):
/// the consideration paid by each buyer + their share, so the sale reconciles with the 1% TDS the buyer
/// deducted u/s 194-IA. Return-scoped (ITR-2/3).
/// </summary>
public class CapitalGainBuyer : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid TaxReturnId { get; set; }

    /// <summary>The immovable-property capital gain this buyer purchased.</summary>
    public Guid CapitalGainId { get; set; }

    public string BuyerName { get; set; } = string.Empty;

    /// <summary>Buyer PAN (preferred) or Aadhaar — at least one identifies the buyer for s.194-IA matching.</summary>
    public string? BuyerPan { get; set; }
    public string? BuyerAadhaar { get; set; }

    /// <summary>This buyer's ownership share of the property (percent; the buyers' shares sum to 100).</summary>
    public decimal PercentageShare { get; set; }

    /// <summary>Consideration paid by this buyer (the buyers' amounts sum to the full consideration).</summary>
    public decimal Amount { get; set; }

    public string AddressOfProperty { get; set; } = string.Empty;

    /// <summary>State code (ITD enum) of the property's location.</summary>
    public string StateCode { get; set; } = string.Empty;

    public int PinCode { get; set; }

    public CapitalGain? CapitalGain { get; set; }
}
