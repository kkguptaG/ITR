namespace TallyG.Tax.Domain.TaxEngine;

/// <summary>
/// Block-of-assets depreciation (s.32) for one rate block. Additions put to use for ≥180 days get the
/// full rate; additions put to use for &lt;180 days get half the rate in the year of addition. Closing
/// WDV = opening WDV + additions − depreciation. This slice models the common case with no sales/transfers
/// in the block (deemed gains u/s 50 are a future addition), so the full-rate base is opening WDV +
/// ≥180-day additions and the half-rate base is the &lt;180-day additions.
/// </summary>
public static class DepreciationCalculator
{
    public sealed record BlockDepreciation(
        decimal OpeningWdv,
        decimal AdditionsAbove180,
        decimal AdditionsBelow180,
        decimal Rate,
        decimal FullRateBase,
        decimal HalfRateBase,
        decimal DepreciationAtFullRate,
        decimal DepreciationAtHalfRate,
        decimal TotalDepreciation,
        decimal ClosingWdv);

    public static BlockDepreciation Compute(decimal openingWdv, decimal additionsAbove180, decimal additionsBelow180, decimal rate)
    {
        var wdv = NonNeg(openingWdv);
        var add180 = NonNeg(additionsAbove180);
        var addLess = NonNeg(additionsBelow180);

        var fullBase = wdv + add180;
        var halfBase = addLess;
        var depFull = Round(fullBase * rate);
        var depHalf = Round(halfBase * (rate / 2m));
        var totalDep = depFull + depHalf;
        var closingWdv = NonNeg(fullBase + halfBase - totalDep);

        return new BlockDepreciation(wdv, add180, addLess, rate, fullBase, halfBase, depFull, depHalf, totalDep, closingWdv);
    }

    private static decimal NonNeg(decimal v) => v < 0m ? 0m : v;

    private static decimal Round(decimal v) => Math.Round(v, MidpointRounding.AwayFromZero);
}
