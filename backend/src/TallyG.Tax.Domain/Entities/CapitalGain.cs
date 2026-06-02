using TallyG.Tax.Domain.Common;
using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Domain.Entities;

/// <summary>Per-transaction / per-scrip-bucket capital gain (Ch.2 §2.5, Ch.3 §3.6).</summary>
public class CapitalGain : BaseEntity, ITenantScoped, ISoftDeletable
{
    public Guid TenantId { get; set; }
    public Guid TaxReturnId { get; set; }

    public CapitalGainAssetType AssetType { get; set; }
    public CapitalGainTerm Term { get; set; }

    /// <summary>e.g. "111A", "112A", "112", "115BBH".</summary>
    public string? TaxSection { get; set; }

    public DateOnly? AcquisitionDate { get; set; }
    public DateOnly? TransferDate { get; set; }

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
