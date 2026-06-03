using TallyG.Tax.Domain.Common;
using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Domain.Entities;

/// <summary>
/// A block of depreciable business assets (the block-of-assets method, s.32) for Schedule DPM. Holds the
/// opening written-down value and the year's additions (split by whether the asset was put to use for
/// 180 days or more — full rate — or less — half rate). Sales/transfers (deemed gains u/s 50) are a
/// future addition. Return-scoped (ITR-3).
/// </summary>
public class DepreciableAsset : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid TaxReturnId { get; set; }

    /// <summary>The asset block (also fixes the depreciation rate).</summary>
    public DepreciableAssetCategory Category { get; set; }

    /// <summary>Opening written-down value of the block on the first day of the year.</summary>
    public decimal OpeningWdv { get; set; }

    /// <summary>Additions put to use for 180 days or more (eligible for the full rate).</summary>
    public decimal AdditionsAbove180Days { get; set; }

    /// <summary>Additions put to use for less than 180 days (eligible for half the rate this year).</summary>
    public decimal AdditionsBelow180Days { get; set; }

    /// <summary>Sale proceeds (money received) on assets of the block transferred during the year. When
    /// these exceed the block's value the excess is a deemed short-term capital gain u/s 50.</summary>
    public decimal SaleProceeds { get; set; }

    /// <summary>Depreciation charged on this block in the BOOKS (the P&amp;L expense). It is added back and
    /// replaced by the s.32 (Income-tax Act) depreciation when reconciling book profit to taxable business
    /// income (Schedule BP). When books and tax depreciation are equal the adjustment is nil.</summary>
    public decimal BookDepreciation { get; set; }

    public TaxReturn? TaxReturn { get; set; }
}
