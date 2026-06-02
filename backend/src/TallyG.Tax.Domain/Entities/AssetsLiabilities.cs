using TallyG.Tax.Domain.Common;

namespace TallyG.Tax.Domain.Entities;

/// <summary>
/// The assessee's Schedule AL declaration — movable assets (by category, at cost) and the liabilities
/// related to them. Mandatory in ITR-2/3 when total income exceeds ₹50 lakh. One declaration per return
/// (upserted). Immovable-property rows (which need a structured address) are a separate, later addition.
/// </summary>
public class AssetsLiabilities : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid TaxReturnId { get; set; }

    // Movable assets at cost (Schedule AL "MovableAsset").
    public decimal BankDeposits { get; set; }
    public decimal SharesAndSecurities { get; set; }
    public decimal InsurancePolicies { get; set; }
    public decimal LoansAndAdvancesGiven { get; set; }
    public decimal CashInHand { get; set; }
    public decimal JewelleryBullion { get; set; }
    public decimal ArtCollections { get; set; }
    public decimal Vehicles { get; set; }

    /// <summary>Liabilities in relation to the declared assets.</summary>
    public decimal Liabilities { get; set; }

    public TaxReturn? TaxReturn { get; set; }
}
