using TallyG.Tax.Domain.Entities;
using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Domain.TaxEngine;

/// <summary>
/// Block-of-assets depreciation (s.32) for one rate block. Additions put to use for ≥180 days get the
/// full rate; additions put to use for &lt;180 days get half the rate in the year of addition. Sale
/// proceeds (money received on transfer) reduce the block — from the full-rate base first; if they exceed
/// the whole block the excess is a deemed short-term capital gain u/s 50 and the block ceases (no
/// depreciation, closing WDV nil). Closing WDV = (opening WDV + additions − sale proceeds) − depreciation.
/// </summary>
public static class DepreciationCalculator
{
    public sealed record BlockDepreciation(
        decimal OpeningWdv,
        decimal AdditionsAbove180,
        decimal AdditionsBelow180,
        decimal Rate,
        decimal SaleProceeds,
        decimal FullRateBase,
        decimal HalfRateBase,
        decimal DepreciationAtFullRate,
        decimal DepreciationAtHalfRate,
        decimal TotalDepreciation,
        decimal DeemedCapitalGain,
        decimal ClosingWdv);

    public static BlockDepreciation Compute(
        decimal openingWdv, decimal additionsAbove180, decimal additionsBelow180, decimal rate, decimal saleProceeds = 0m)
    {
        var wdv = NonNeg(openingWdv);
        var add180 = NonNeg(additionsAbove180);
        var addLess = NonNeg(additionsBelow180);
        var proceeds = NonNeg(saleProceeds);

        var fullBase = wdv + add180;
        var halfBase = addLess;
        var blockValue = fullBase + halfBase;

        // Money received reduces the block, from the full-rate base first; the excess hits the half-rate base.
        var adjFull = NonNeg(fullBase - proceeds);
        var adjHalf = NonNeg(halfBase - NonNeg(proceeds - fullBase));
        var deemedGain = NonNeg(proceeds - blockValue);   // s.50 deemed STCG when proceeds exceed the block

        var depFull = Round(adjFull * rate);
        var depHalf = Round(adjHalf * (rate / 2m));
        var totalDep = depFull + depHalf;
        var closingWdv = NonNeg(adjFull + adjHalf - totalDep);

        return new BlockDepreciation(wdv, add180, addLess, rate, proceeds, fullBase, halfBase, depFull, depHalf, totalDep, deemedGain, closingWdv);
    }

    /// <summary>
    /// Total deemed short-term capital gain u/s 50 across all depreciable blocks — the sale proceeds of a
    /// block in excess of its value (opening WDV + additions). Single source of truth shared by the tax
    /// input factory (which taxes it as an STCG) and the generator (Schedule CG / DCG). Rate-independent.
    /// </summary>
    public static decimal TotalDeemedCapitalGain(IEnumerable<DepreciableAsset> blocks)
        => blocks.GroupBy(b => b.Category)
                 .Sum(g => NonNeg(g.Sum(b => b.SaleProceeds)
                                  - g.Sum(b => b.OpeningWdv + b.AdditionsAbove180Days + b.AdditionsBelow180Days)));

    /// <summary>The s.32 written-down-value depreciation rate for a block category (the rate is encoded in the
    /// category name). Single source shared by the generator (Schedule DPM/DOA/DEP) and the BP reconciliation.</summary>
    public static decimal RateFor(DepreciableAssetCategory category) => category switch
    {
        DepreciableAssetCategory.PlantMachinery15 => 0.15m,
        DepreciableAssetCategory.PlantMachinery30 => 0.30m,
        DepreciableAssetCategory.PlantMachinery40 => 0.40m,
        DepreciableAssetCategory.PlantMachinery45 => 0.45m,
        DepreciableAssetCategory.Building5 => 0.05m,
        DepreciableAssetCategory.Building10 => 0.10m,
        DepreciableAssetCategory.Building40 => 0.40m,
        DepreciableAssetCategory.FurnitureFittings10 => 0.10m,
        DepreciableAssetCategory.IntangibleAssets25 => 0.25m,
        DepreciableAssetCategory.Ships20 => 0.20m,
        _ => 0m,
    };

    /// <summary>
    /// Total depreciation allowable u/s 32 across all blocks (each category at its WDV rate, ≥/&lt;180-day
    /// additions handled, sales reducing the block). The "depreciation as per the Income-tax Act" that is
    /// allowed in place of the book depreciation when reconciling business income (Schedule BP). Shared by
    /// the input factory (book-vs-tax depreciation adjustment) and the generator (Schedule DEP total).
    /// </summary>
    public static decimal TotalDepreciation(IEnumerable<DepreciableAsset> blocks)
        => blocks.GroupBy(b => b.Category)
                 .Sum(g => Compute(g.Sum(b => b.OpeningWdv), g.Sum(b => b.AdditionsAbove180Days),
                                   g.Sum(b => b.AdditionsBelow180Days), RateFor(g.Key), g.Sum(b => b.SaleProceeds)).TotalDepreciation);

    private static decimal NonNeg(decimal v) => v < 0m ? 0m : v;

    private static decimal Round(decimal v) => Math.Round(v, MidpointRounding.AwayFromZero);
}
