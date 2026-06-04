using TallyG.Tax.Domain.Common;
using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Domain.Entities;

/// <summary>Per-transaction / per-scrip-bucket capital gain (Ch.2 §2.5, Ch.3 §3.6).</summary>
public class CapitalGain : BaseEntity, ITenantScoped, ISoftDeletable
{
    public Guid TenantId { get; set; }
    public Guid TaxReturnId { get; set; }

    public CapitalGainAssetType AssetType { get; set; }

    /// <summary>Fine-grained asset sub-type (ESOP/RSU/SGB/foreign share/…). Optional. When set, the broad
    /// <see cref="AssetType"/> is derived from it via <see cref="TaxEngine.CapitalGainTaxonomy.CategoryOf"/>
    /// at save time, so the engine keeps routing off the category. Drives UI guidance + schedule nuance.</summary>
    public CapitalGainSubType? SubType { get; set; }

    public CapitalGainTerm Term { get; set; }

    /// <summary>STT was paid on the transfer — gates s.111A/112A equity treatment (Ch.3 §3.6.1).</summary>
    public bool SttPaid { get; set; }

    /// <summary>TDS deducted on this sale (s.194-IA property 1% / s.195 NRI), credited against the liability.</summary>
    public decimal TdsOnSale { get; set; }
    public string? TdsSection { get; set; }

    /// <summary>The assessee's ownership share for a jointly-held asset (percent; default 100). The gain is
    /// apportioned to this share before tax (each co-owner returns their own portion).</summary>
    public decimal CoOwnerPercent { get; set; } = 100m;

    /// <summary>e.g. "111A", "112A", "112", "115BBH".</summary>
    public string? TaxSection { get; set; }

    /// <summary>How the asset was acquired. Gift / Inheritance / Will step in the previous owner's cost
    /// (s.49(1)) and holding period (s.2(42A)) — see <see cref="PreviousOwnerCost"/> / <see cref="PreviousOwnerAcquisitionDate"/>.</summary>
    public CapitalGainAcquisitionMode AcquisitionMode { get; set; } = CapitalGainAcquisitionMode.Purchase;

    public DateOnly? AcquisitionDate { get; set; }
    public DateOnly? TransferDate { get; set; }

    /// <summary>For Gift / Inheritance / Will: the date the PREVIOUS owner acquired the asset — the holding
    /// period (and indexation base year) counts from here (s.2(42A) / s.55). Null ⇒ use <see cref="AcquisitionDate"/>.</summary>
    public DateOnly? PreviousOwnerAcquisitionDate { get; set; }

    /// <summary>For Gift / Inheritance / Will: the cost to the previous owner (s.49(1)). 0 ⇒ use <see cref="CostOfAcquisition"/>.</summary>
    public decimal PreviousOwnerCost { get; set; }

    /// <summary>True when this is RURAL agricultural land — not a "capital asset" u/s 2(14), so the gain is
    /// fully exempt and excluded from the computation. Urban agricultural land remains taxable (eligible for s.54B).</summary>
    public bool IsRuralAgriculturalLand { get; set; }

    public decimal SalePrice { get; set; }
    public decimal CostOfAcquisition { get; set; }
    public decimal IndexedCost { get; set; }
    public decimal CostOfImprovement { get; set; }
    public decimal ExpensesOnTransfer { get; set; }

    public string? ExemptionSection { get; set; }
    public decimal ExemptionAmount { get; set; }

    /// <summary>Amount reinvested (new house u/s 54/54F, or NHAI/REC bonds u/s 54EC) driving the computed LTCG exemption.</summary>
    public decimal ReinvestmentAmount { get; set; }

    public decimal Gain { get; set; }
    public string? Isin { get; set; }

    /// <summary>Fair market value on 31-Jan-2018 (per unit × units / total) for s.112A grandfathering
    /// (s.55(2)(ac)) of listed equity / equity-MF acquired on or before that date. 0 ⇒ not applicable.</summary>
    public decimal FairMarketValue31Jan2018 { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public TaxReturn? TaxReturn { get; set; }
}
