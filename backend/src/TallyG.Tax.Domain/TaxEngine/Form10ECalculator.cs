using System;
using System.Collections.Generic;
using System.Linq;

namespace TallyG.Tax.Domain.TaxEngine;

/// <summary>One earlier year that a salary arrears / advance payment relates to (Form 10E, Annexure I,
/// Table A): the year's own total income and the portion of the arrears attributable to it.</summary>
public sealed record ArrearYearAllocation(string FinancialYear, decimal TotalIncomeOfThatYear, decimal ArrearsForThatYear);

/// <summary>The Form 10E worked table: tax on the current year with/without the arrears, the resulting
/// extra tax this year vs. the extra tax across the earlier years, and the s.89(1) relief.</summary>
public sealed record Form10EResult(
    decimal TaxOnCurrentInclArrears,
    decimal TaxOnCurrentExclArrears,
    decimal AdditionalTaxCurrentYear,
    decimal AdditionalTaxEarlierYears,
    decimal ReliefUs89);

/// <summary>
/// Form 10E — relief u/s 89(1) for salary received in arrears or advance. The relief is the extra tax the
/// arrears push into the CURRENT year minus the extra tax those same arrears would have attracted in the
/// EARLIER years they relate to: relief = max(0, ΔtaxThisYear − Σ ΔtaxEarlierYears). It is positive only
/// when bunching the arrears into one year crosses higher slabs than spreading them would have.
///
/// Uses the OLD-regime individual (&lt;60) slab structure — ₹2.5L/5%/20%/30%, the ₹12,500 s.87A rebate up
/// to ₹5L, and 4% cess — which has been stable from AY2020-21, the overwhelmingly common case for arrears
/// (the new regime rarely applies to back-pay). Per-year rates for older years / senior slabs / the new
/// regime are a future refinement; the relief formula itself is exact.
/// </summary>
public static class Form10ECalculator
{
    // Old-regime individual (<60) slabs, stable AY2020-21..AY2025-26.
    private static readonly (decimal Upto, decimal Rate)[] OldRegimeSlabs =
    {
        (250_000m, 0m),
        (500_000m, 0.05m),
        (1_000_000m, 0.20m),
        (decimal.MaxValue, 0.30m),
    };

    private const decimal Rebate87AIncomeCap = 500_000m;
    private const decimal Rebate87AMax = 12_500m;
    private const decimal CessRate = 0.04m;

    /// <summary>Old-regime tax (incl. s.87A rebate and 4% cess) on a taxable income, rounded to the rupee.</summary>
    public static decimal TaxOnIncome(decimal taxableIncome)
    {
        var income = Math.Max(0m, decimal.Round(taxableIncome, MidpointRounding.AwayFromZero));

        decimal tax = 0m, lower = 0m;
        foreach (var (upto, rate) in OldRegimeSlabs)
        {
            if (income <= lower)
            {
                break;
            }

            var band = Math.Min(income, upto) - lower;
            if (band > 0m)
            {
                tax += band * rate;
            }

            lower = upto;
        }

        // s.87A rebate — resident individuals with total income up to ₹5,00,000 (old regime).
        if (income <= Rebate87AIncomeCap)
        {
            tax = Math.Max(0m, tax - Rebate87AMax);
        }

        return decimal.Round(tax * (1m + CessRate), MidpointRounding.AwayFromZero);   // + 4% health & education cess
    }

    /// <summary>Compute the s.89(1) relief from the current year's total income (INCLUDING the arrears) and
    /// the per-earlier-year allocation of those arrears.</summary>
    public static Form10EResult Compute(decimal currentYearTotalIncome, IReadOnlyList<ArrearYearAllocation> arrears)
    {
        arrears ??= Array.Empty<ArrearYearAllocation>();
        var totalArrears = arrears.Sum(a => Math.Max(0m, a.ArrearsForThatYear));

        var taxWith = TaxOnIncome(currentYearTotalIncome);
        var taxWithout = TaxOnIncome(currentYearTotalIncome - totalArrears);
        var additionalCurrent = Math.Max(0m, taxWith - taxWithout);

        decimal additionalEarlier = 0m;
        foreach (var a in arrears)
        {
            var arrear = Math.Max(0m, a.ArrearsForThatYear);
            if (arrear <= 0m)
            {
                continue;
            }

            var withArrear = TaxOnIncome(a.TotalIncomeOfThatYear + arrear);
            var withoutArrear = TaxOnIncome(a.TotalIncomeOfThatYear);
            additionalEarlier += Math.Max(0m, withArrear - withoutArrear);
        }

        var relief = Math.Max(0m, additionalCurrent - additionalEarlier);
        return new Form10EResult(taxWith, taxWithout, additionalCurrent, additionalEarlier, relief);
    }
}
