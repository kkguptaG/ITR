using FluentAssertions;
using TallyG.Tax.Domain.TaxEngine;
using Xunit;

namespace TallyG.Tax.Tests.TaxEngine;

/// <summary>
/// Form 10E — s.89(1) salary-arrears relief. Worked against the old-regime slabs (₹2.5L/5/20/30, ₹12,500
/// 87A up to ₹5L, 4% cess), hand-computed to the rupee.
/// </summary>
public class Form10ECalculatorTests
{
    [Theory]
    [InlineData(250_000, 0)]          // within the basic exemption
    [InlineData(500_000, 0)]          // ₹12,500 slab tax fully wiped by the 87A rebate
    [InlineData(1_000_000, 117_000)]  // 12,500 + 1,00,000 = 1,12,500 + 4% cess
    [InlineData(1_200_000, 179_400)]  // + 2L @ 30% = 1,72,500 + 4% cess
    public void Old_regime_tax_matches_hand_computation(decimal income, decimal expectedTax)
        => Form10ECalculator.TaxOnIncome(income).Should().Be(expectedTax);

    [Fact]
    public void Arrears_bunched_into_a_higher_slab_year_attract_relief()
    {
        // ₹2L of arrears (relating to FY2021-22, when income was ₹4L) is received this year, lifting the
        // current total income to ₹12L. Bunching crosses the 20%→30% slabs, so s.89 relief is due.
        var result = Form10ECalculator.Compute(
            currentYearTotalIncome: 1_200_000m,
            arrears: new[] { new ArrearYearAllocation("2021-22", TotalIncomeOfThatYear: 400_000m, ArrearsForThatYear: 200_000m) });

        // Current year: tax(12L) − tax(10L) = 1,79,400 − 1,17,000 = ₹62,400 extra.
        result.AdditionalTaxCurrentYear.Should().Be(62_400m);
        // FY2021-22: tax(6L) − tax(4L) = 33,800 − 0 = ₹33,800 extra (the ₹4L year paid nil after 87A).
        result.AdditionalTaxEarlierYears.Should().Be(33_800m);
        // Relief = 62,400 − 33,800.
        result.ReliefUs89.Should().Be(28_600m);
    }

    [Fact]
    public void No_relief_when_both_years_are_already_in_the_top_bracket()
    {
        // The earlier year was already at ₹18L (30% bracket); bunching ₹2L into the current ₹20L doesn't
        // change the marginal rate, so the extra tax is identical either way → no relief.
        var result = Form10ECalculator.Compute(
            currentYearTotalIncome: 2_000_000m,
            arrears: new[] { new ArrearYearAllocation("2022-23", TotalIncomeOfThatYear: 1_800_000m, ArrearsForThatYear: 200_000m) });

        result.AdditionalTaxCurrentYear.Should().Be(result.AdditionalTaxEarlierYears);
        result.ReliefUs89.Should().Be(0m);
    }

    [Fact]
    public void Relief_sums_across_multiple_earlier_years()
    {
        // ₹3L arrears split across two earlier years (₹1L → a ₹4L year, ₹2L → a ₹3L year); current income ₹13L.
        var result = Form10ECalculator.Compute(
            currentYearTotalIncome: 1_300_000m,
            arrears: new[]
            {
                new ArrearYearAllocation("2020-21", 400_000m, 100_000m),
                new ArrearYearAllocation("2021-22", 300_000m, 200_000m),
            });

        // Current: tax(13L) − tax(10L) = 2,10,600 − 1,17,000 = ₹93,600.
        result.AdditionalTaxCurrentYear.Should().Be(93_600m);
        // Both earlier years stayed within ₹5L after the allocation → nil tax (87A), so no earlier extra tax.
        result.AdditionalTaxEarlierYears.Should().Be(0m);
        result.ReliefUs89.Should().Be(93_600m);
    }
}
