using FluentAssertions;
using TallyG.Tax.Domain.Enums;
using TallyG.Tax.Domain.TaxEngine;
using Xunit;

namespace TallyG.Tax.Tests.TaxEngine;

/// <summary>
/// s.112A grandfathering (s.55(2)(ac)) for listed equity / equity MF acquired on or before 31-Jan-2018:
/// the cost of acquisition is lifted to the higher of (actual cost) and (lower of 31-Jan-2018 FMV and the
/// full value of consideration). Exercises the FairMarketValueOnGrandfatherDate input that
/// TaxComputationInputFactory now feeds from the captured CapitalGain.
/// </summary>
public class CapitalGain112AGrandfatherTests
{
    private static readonly CapitalGainRules Rules = RuleSet.Parse(RuleSetFixture.Ay2025_26Json).CapitalGains;

    private static CapitalGainInput Equity112A(decimal sale, decimal cost, decimal? fmv, DateOnly? acquired)
        => new(CapitalGainAssetType.ListedEquity, CapitalGainTerm.Long, "112A",
            SaleConsideration: sale, CostOfAcquisition: cost, CostOfImprovement: 0m, ExpensesOnTransfer: 0m,
            ExemptionAmount: 0m, AcquisitionDate: acquired, TransferDate: null,
            FairMarketValueOnGrandfatherDate: fmv, IndexedCost: null,
            ExemptionSection: null, ReinvestmentAmount: 0m);

    [Fact]
    public void Pre2018_equity_with_fmv_uses_the_grandfathered_cost()
    {
        // Sale ₹10L, actual cost ₹2L, FMV-31-Jan-2018 ₹7L, acquired 2015 ⇒ cost = max(2L, min(7L, 10L)) = ₹7L
        // ⇒ gross 112A LTCG = ₹3L (NOT ₹8L).
        var r = CapitalGainsCalculator.Compute(new[] { Equity112A(1_000_000m, 200_000m, fmv: 700_000m, acquired: new DateOnly(2015, 1, 1)) }, Rules);

        r.Buckets.Ltcg112AGross.Should().Be(300_000m);
    }

    [Fact]
    public void Post2018_equity_ignores_fmv_and_uses_actual_cost()
    {
        // Acquired after 31-Jan-2018 ⇒ no grandfathering even if an FMV is present ⇒ gross = ₹10L − ₹2L = ₹8L.
        var r = CapitalGainsCalculator.Compute(new[] { Equity112A(1_000_000m, 200_000m, fmv: 700_000m, acquired: new DateOnly(2022, 1, 1)) }, Rules);

        r.Buckets.Ltcg112AGross.Should().Be(800_000m);
    }

    [Fact]
    public void Grandfathered_cost_never_falls_below_actual_cost()
    {
        // FMV ₹1.5L is BELOW the actual cost ₹2L ⇒ cost stays ₹2L (the "higher of" arm) ⇒ gross = ₹8L.
        var r = CapitalGainsCalculator.Compute(new[] { Equity112A(1_000_000m, 200_000m, fmv: 150_000m, acquired: new DateOnly(2015, 1, 1)) }, Rules);

        r.Buckets.Ltcg112AGross.Should().Be(800_000m);
    }
}
