using FluentAssertions;
using TallyG.Tax.Domain.Enums;
using TallyG.Tax.Domain.TaxEngine;
using Xunit;

namespace TallyG.Tax.Tests.TaxEngine;

/// <summary>
/// Multiple-acquisition-lot capital gains (Ch.3 §3.6): each lot derives its own term / indexation /
/// grandfathering, so one sale can straddle short- and long-term.
/// </summary>
public class CapitalGainLotsTests
{
    private static readonly CapitalGainRules Rules = RuleSet.Parse(RuleSetFixture.Ay2025_26Json).CapitalGains;

    [Fact]
    public void Parse_reads_the_lot_json_array()
    {
        const string json = """
        [
          { "acquisitionDate": "2015-01-01", "quantity": 100, "cost": 50000, "fairMarketValue31Jan2018": 0 },
          { "acquisitionDate": "2024-03-01", "quantity": 50, "cost": 80000 }
        ]
        """;

        var lots = CapitalGainLots.Parse(json);

        lots.Should().HaveCount(2);
        lots[0].AcquisitionDate.Should().Be(new DateOnly(2015, 1, 1));
        lots[0].Quantity.Should().Be(100m);
        lots[0].Cost.Should().Be(50000m);
        lots[1].Quantity.Should().Be(50m);
    }

    [Fact]
    public void Parse_returns_empty_for_blank_or_malformed_json()
    {
        CapitalGainLots.Parse(null).Should().BeEmpty();
        CapitalGainLots.Parse("not json").Should().BeEmpty();
        CapitalGainLots.Parse("{}").Should().BeEmpty();
    }

    [Fact]
    public void Expand_splits_a_sale_pro_rata_and_derives_each_lots_own_term()
    {
        var lots = new[]
        {
            new CapitalGainLot(new DateOnly(2015, 1, 1), 100m, 50000m, 0m), // long-term (held ~9y)
            new CapitalGainLot(new DateOnly(2024, 3, 1), 50m, 80000m, 0m),  // short-term (held ~6m)
        };

        var inputs = CapitalGainLots.Expand(
            CapitalGainAssetType.ListedEquity, taxSection: null,
            totalSale: 300000m, totalImprovement: 0m, totalExpenses: 0m,
            transferDate: new DateOnly(2024, 9, 1), lots, Rules);

        inputs.Should().HaveCount(2);
        var longLot = inputs.Single(i => i.Term == CapitalGainTerm.Long);
        var shortLot = inputs.Single(i => i.Term == CapitalGainTerm.Short);
        longLot.SaleConsideration.Should().Be(200000m); // 100/150 of 3,00,000
        longLot.CostOfAcquisition.Should().Be(50000m);
        shortLot.SaleConsideration.Should().Be(100000m); // 50/150 of 3,00,000
        shortLot.CostOfAcquisition.Should().Be(80000m);
    }

    [Fact]
    public void A_two_lot_equity_sale_splits_into_112A_long_and_111A_short_buckets()
    {
        var lots = new[]
        {
            new CapitalGainLot(new DateOnly(2015, 1, 1), 100m, 50000m, 0m),
            new CapitalGainLot(new DateOnly(2024, 3, 1), 50m, 80000m, 0m),
        };
        var inputs = CapitalGainLots.Expand(
            CapitalGainAssetType.ListedEquity, null, 300000m, 0m, 0m, new DateOnly(2024, 9, 1), lots, Rules);

        var r = CapitalGainsCalculator.Compute(inputs, Rules);

        r.Buckets.Ltcg112AGross.Should().Be(150000m); // 2,00,000 − 50,000
        r.Buckets.Stcg111A.Should().Be(20000m);       // 1,00,000 − 80,000
    }
}
