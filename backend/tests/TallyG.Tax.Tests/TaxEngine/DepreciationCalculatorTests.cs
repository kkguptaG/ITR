using FluentAssertions;
using TallyG.Tax.Domain.TaxEngine;
using Xunit;

namespace TallyG.Tax.Tests.TaxEngine;

/// <summary>
/// Block-of-assets depreciation (s.32): full rate on opening WDV + ≥180-day additions, half rate on
/// &lt;180-day additions, closing WDV = opening + additions − depreciation.
/// </summary>
public class DepreciationCalculatorTests
{
    [Fact]
    public void Half_rate_applies_to_additions_used_under_180_days()
    {
        // 15% block: WDV ₹10L + ₹2L (≥180d) at 15%, + ₹1L (<180d) at 7.5%.
        var d = DepreciationCalculator.Compute(1_000_000m, 200_000m, 100_000m, 0.15m);

        d.FullRateBase.Should().Be(1_200_000m);
        d.HalfRateBase.Should().Be(100_000m);
        d.DepreciationAtFullRate.Should().Be(180_000m);   // 12L × 15%
        d.DepreciationAtHalfRate.Should().Be(7_500m);     // 1L × 7.5%
        d.TotalDepreciation.Should().Be(187_500m);
        d.ClosingWdv.Should().Be(1_112_500m);             // 13L − 1,87,500
    }

    [Fact]
    public void Full_block_with_no_additions_depreciates_at_the_block_rate()
    {
        var d = DepreciationCalculator.Compute(500_000m, 0m, 0m, 0.40m);

        d.TotalDepreciation.Should().Be(200_000m);   // 5L × 40%
        d.ClosingWdv.Should().Be(300_000m);
    }

    [Fact]
    public void Sale_below_block_value_reduces_the_base_with_no_deemed_gain()
    {
        // WDV 10L, sale proceeds 3L → depreciation on 7L @ 15%; no deemed gain; block continues.
        var d = DepreciationCalculator.Compute(1_000_000m, 0m, 0m, 0.15m, saleProceeds: 300_000m);

        d.DeemedCapitalGain.Should().Be(0m);
        d.TotalDepreciation.Should().Be(105_000m);   // 7L × 15%
        d.ClosingWdv.Should().Be(595_000m);          // 7L − 1,05,000
    }

    [Fact]
    public void Sale_exceeding_the_block_yields_a_deemed_short_term_gain_us_50()
    {
        // WDV 10L (+2L additions = 12L block), sold for 15L → block ceases, deemed STCG ₹3L, no depreciation.
        var d = DepreciationCalculator.Compute(1_000_000m, 200_000m, 0m, 0.15m, saleProceeds: 1_500_000m);

        d.DeemedCapitalGain.Should().Be(300_000m);   // 15L − 12L
        d.TotalDepreciation.Should().Be(0m);
        d.ClosingWdv.Should().Be(0m);
    }
}
