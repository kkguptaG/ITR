using TallyG.Tax.Domain.Entities;

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

    private static decimal NonNeg(decimal v) => v < 0m ? 0m : v;

    private static decimal Round(decimal v) => Math.Round(v, MidpointRounding.AwayFromZero);
}
