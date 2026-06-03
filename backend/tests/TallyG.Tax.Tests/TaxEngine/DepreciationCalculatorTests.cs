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
}
