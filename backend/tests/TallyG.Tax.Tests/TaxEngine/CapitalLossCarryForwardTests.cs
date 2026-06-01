using FluentAssertions;
using TallyG.Tax.Domain.Enums;
using TallyG.Tax.Domain.TaxEngine;
using Xunit;

namespace TallyG.Tax.Tests.TaxEngine;

/// <summary>
/// CURRENT-year capital-loss intra-head set-off (s.70) and carry-forward (s.74), as opposed to the
/// brought-forward path in <see cref="CapitalLossSetOffTests"/>. s.70(2): STCL → STCG then LTCG;
/// s.70(3): LTCL → LTCG only. VDA (s.115BBH) losses are isolated — never set off, never carried.
/// AY2025-26 fixture: STCG-111A @15%, LTCG-112/112A @12.5%, cess 4%.
/// </summary>
public class CapitalLossCarryForwardTests
{
    private static readonly CapitalGainRules Rules = RuleSet.Parse(RuleSetFixture.Ay2025_26Json).CapitalGains;
    private readonly ITaxCalculator _engine = new TaxCalculator();

    private static CapitalGainInput Listed(CapitalGainTerm term, decimal sale, decimal cost)
        => new(CapitalGainAssetType.ListedEquity, term, null, sale, cost, 0m, 0m, 0m, null, null);

    // Gold short-term is taxed at slab (a non-111A STCG bucket).
    private static CapitalGainInput GoldShort(decimal sale, decimal cost)
        => new(CapitalGainAssetType.Gold, CapitalGainTerm.Short, null, sale, cost, 0m, 0m, 0m, null, null);

    private static CapitalGainInput UnlistedLong(decimal sale, decimal cost)
        => new(CapitalGainAssetType.UnlistedShares, CapitalGainTerm.Long, null, sale, cost, 0m, 0m, 0m, null, null);

    private static CapitalGainInput Vda(decimal sale, decimal cost)
        => new(CapitalGainAssetType.CryptoVda, CapitalGainTerm.Short, null, sale, cost, 0m, 0m, 0m, null, null);

    [Fact]
    public void Short_term_loss_sets_off_against_111A_short_term_gain()
    {
        // Slab STCL ₹2,00,000 (gold) + s.111A STCG ₹3,00,000 → 111A reduced to ₹1,00,000; nothing carries.
        var r = CapitalGainsCalculator.Compute(new[] { GoldShort(100_000m, 300_000m), Listed(CapitalGainTerm.Short, 400_000m, 100_000m) }, Rules);

        r.Buckets.Stcg111A.Should().Be(100_000m);
        r.Buckets.SlabRateGains.Should().Be(0m);
        r.CurrentShortTermLossCarried.Should().Be(0m);
    }

    [Fact]
    public void Short_term_loss_sets_off_against_long_term_gain_then_excess_carries_forward()
    {
        // Slab STCL ₹2,00,000 + LTCG-112 ₹1,50,000 → STCL absorbs the LTCG, ₹50,000 STCL carries forward.
        var r = CapitalGainsCalculator.Compute(new[] { GoldShort(100_000m, 300_000m), UnlistedLong(250_000m, 100_000m) }, Rules);

        r.Buckets.Ltcg112.Should().Be(0m);
        r.CurrentShortTermLossCarried.Should().Be(50_000m);
        r.CurrentLongTermLossCarried.Should().Be(0m);
    }

    [Fact]
    public void Long_term_loss_cannot_set_off_against_short_term_gain_and_carries_forward()
    {
        // LTCL ₹2,00,000 (unlisted) + s.111A STCG ₹3,00,000 → LTCL must NOT touch the STCG (s.70(3));
        // the full ₹2,00,000 LTCL carries forward, the STCG stays taxable.
        var r = CapitalGainsCalculator.Compute(new[] { UnlistedLong(100_000m, 300_000m), Listed(CapitalGainTerm.Short, 400_000m, 100_000m) }, Rules);

        r.Buckets.Stcg111A.Should().Be(300_000m);
        r.CurrentLongTermLossCarried.Should().Be(200_000m);
        r.CurrentShortTermLossCarried.Should().Be(0m);
    }

    [Fact]
    public void Vda_loss_is_isolated_neither_set_off_nor_carried()
    {
        // A VDA loss (s.115BBH) cannot reduce other gains and does not carry forward; the STCG stands.
        var r = CapitalGainsCalculator.Compute(new[] { Vda(100_000m, 300_000m), Listed(CapitalGainTerm.Short, 300_000m, 100_000m) }, Rules);

        r.Buckets.Crypto115Bbh.Should().Be(0m);
        r.Buckets.Stcg111A.Should().Be(200_000m);
        r.CurrentShortTermLossCarried.Should().Be(0m);
        r.CurrentLongTermLossCarried.Should().Be(0m);
    }

    [Fact]
    public void Engine_surfaces_current_year_stcl_carry_forward()
    {
        // Slab STCL ₹2,00,000 + s.111A STCG ₹50,000 → ₹50,000 set off, ₹1,50,000 STCL carried; tax nil.
        var input = new TaxComputationInput
        {
            AssessmentYearCode = "AY2025-26",
            RuleSetVersion = "1.0.0",
            RulesJson = RuleSetFixture.Ay2025_26Json,
            Age = 35,
            CapitalGains = new[] { GoldShort(100_000m, 300_000m), Listed(CapitalGainTerm.Short, 350_000m, 300_000m) },
        };

        var r = _engine.Compute(input, Regime.New);

        r.TotalTax.Should().Be(0m);
        r.ShortTermCapitalLossCarriedForward.Should().Be(150_000m);
        r.LongTermCapitalLossCarriedForward.Should().Be(0m);
        r.Trace.Should().Contain(t => t.Step == "CG.StclCarryForward" && t.Amount == 150_000m);
    }
}
