using FluentAssertions;
using TallyG.Tax.Domain.Enums;
using TallyG.Tax.Domain.TaxEngine;
using Xunit;

namespace TallyG.Tax.Tests.TaxEngine;

/// <summary>
/// Brought-forward capital-loss set-off against the rate-specific buckets. AY2025-26 fixture:
/// STCG-111A @ 15%, LTCG-112 @ 12.5%, cess 4%. STCL can hit STCG + LTCG; LTCL only LTCG.
/// </summary>
public class CapitalLossSetOffTests
{
    private readonly ITaxCalculator _engine = new TaxCalculator();

    private static CapitalGainInput Equity(CapitalGainTerm term, decimal sale, decimal cost)
        => new(CapitalGainAssetType.ListedEquity, term, null, sale, cost, 0m, 0m, 0m, null, null);

    private static CapitalGainInput UnlistedLong(decimal sale, decimal cost)
        => new(CapitalGainAssetType.UnlistedShares, CapitalGainTerm.Long, null, sale, cost, 0m, 0m, 0m, null, null);

    private static TaxComputationInput Build(decimal bfStcl, decimal bfLtcl, params CapitalGainInput[] gains)
        => new()
        {
            AssessmentYearCode = "AY2025-26",
            RuleSetVersion = "1.0.0",
            RulesJson = RuleSetFixture.Ay2025_26Json,
            Age = 35,
            CapitalGains = gains,
            BroughtForwardShortTermCapitalLoss = bfStcl,
            BroughtForwardLongTermCapitalLoss = bfLtcl,
        };

    [Fact]
    public void Brought_forward_STCL_sets_off_against_STCG_111A()
    {
        // STCG-111A ₹2,00,000; b/f STCL ₹50,000 → taxed on ₹1,50,000 @ 15% + 4% cess.
        var r = _engine.Compute(Build(bfStcl: 50_000m, bfLtcl: 0m, Equity(CapitalGainTerm.Short, 300_000m, 100_000m)), Regime.New);

        r.TotalTax.Should().Be(23_400m); // 1,50,000 × 15% = 22,500 + 900 cess
        r.Trace.Should().Contain(t => t.Step == "CG.BfStclSetOff" && t.Amount == 50_000m);
    }

    [Fact]
    public void Brought_forward_LTCL_sets_off_against_LTCG_112()
    {
        // LTCG-112 ₹4,00,000; b/f LTCL ₹1,00,000 → taxed on ₹3,00,000 @ 12.5% + 4% cess.
        var r = _engine.Compute(Build(bfStcl: 0m, bfLtcl: 100_000m, UnlistedLong(500_000m, 100_000m)), Regime.New);

        r.TotalTax.Should().Be(39_000m); // 3,00,000 × 12.5% = 37,500 + 1,500 cess
        r.Trace.Should().Contain(t => t.Step == "CG.BfLtclSetOff" && t.Amount == 100_000m);
    }

    [Fact]
    public void Brought_forward_LTCL_cannot_set_off_against_STCG()
    {
        // Only STCG-111A this year; an LTCL must NOT touch it — it carries forward in full.
        var r = _engine.Compute(Build(bfStcl: 0m, bfLtcl: 100_000m, Equity(CapitalGainTerm.Short, 300_000m, 100_000m)), Regime.New);

        r.TotalTax.Should().Be(31_200m); // full 2,00,000 × 15% = 30,000 + 1,200 cess
        r.Trace.Should().Contain(t => t.Step == "CG.BfLtclCarryForward" && t.Amount == 100_000m);
    }
}
