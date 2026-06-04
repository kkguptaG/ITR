using FluentAssertions;
using TallyG.Tax.Domain.Enums;
using TallyG.Tax.Domain.TaxEngine;
using Xunit;

namespace TallyG.Tax.Tests.TaxEngine;

/// <summary>
/// ComputationResult exposes a rate-wise split of capital-gains + casual income (the ITD Schedule SI
/// "income chargeable at special rates" block) plus the normal-vs-special tax split, so the computation
/// dashboard can itemise each bucket the way a CA-grade sheet does. These assert the split is populated,
/// reconciles to the Capital-Gains / Other-Sources head totals, and that the tax split reconciles to
/// tax-before-rebate. Computed against the AY2025-26 fixture (s.111A 15%, s.112A 12.5% over ₹1.25L exempt).
/// </summary>
public class SpecialIncomeBreakdownTests
{
    private readonly ITaxCalculator _engine = new TaxCalculator();

    private static CapitalGainInput ListedEquity(CapitalGainTerm term, decimal sale, decimal cost)
        => new(CapitalGainAssetType.ListedEquity, term, null,
            SaleConsideration: sale, CostOfAcquisition: cost, CostOfImprovement: 0m, ExpensesOnTransfer: 0m,
            ExemptionAmount: 0m, AcquisitionDate: null, TransferDate: null);

    [Fact]
    public void Capital_gains_split_into_rate_buckets_that_reconcile_to_the_head()
    {
        var input = RuleSetFixture.Salaried(1_000_000m) with
        {
            CapitalGains = new[]
            {
                ListedEquity(CapitalGainTerm.Short, 300_000m, 200_000m), // STCG s.111A gain ₹1,00,000
                ListedEquity(CapitalGainTerm.Long, 500_000m, 200_000m),  // LTCG s.112A gain ₹3,00,000 − ₹1.25L exempt
            },
        };

        var r = _engine.Compute(input, Regime.New);
        var si = r.SpecialIncome;

        si.Stcg111A.Should().Be(100_000m);
        si.Ltcg112A.Should().Be(175_000m); // 3,00,000 − 1,25,000 (s.112A exemption)
        si.Ltcg112.Should().Be(0m);
        si.Vda115BBH.Should().Be(0m);

        // The dashboard shows these as sub-lines under "Capital Gains" — they must sum to the head.
        (si.SlabRateCapitalGains + si.Stcg111A + si.Ltcg112A + si.Ltcg112 + si.Vda115BBH)
            .Should().Be(r.CapitalGainsNetIncome, "the rate sub-lines must reconcile to the capital-gains head");
    }

    [Fact]
    public void Winnings_115BB_populate_the_casual_bucket_and_the_other_sources_head()
    {
        var input = RuleSetFixture.Salaried(1_000_000m) with
        {
            OtherIncomes = new[] { new OtherIncomeInput("Lottery", 50_000m, "lottery_115bb") },
        };

        var r = _engine.Compute(input, Regime.New);

        r.SpecialIncome.Casual115BB.Should().Be(50_000m);
        r.OtherSourcesNetIncome.Should().Be(50_000m); // the winnings flow into the Other-Sources head
    }

    [Fact]
    public void Normal_and_special_tax_split_reconciles_to_tax_before_rebate()
    {
        var input = RuleSetFixture.Salaried(1_000_000m) with
        {
            CapitalGains = new[] { ListedEquity(CapitalGainTerm.Short, 500_000m, 200_000m) }, // STCG s.111A ₹3,00,000
        };

        var r = _engine.Compute(input, Regime.New);

        r.TaxAtSpecialRates.Should().BeGreaterThan(0m);
        (r.TaxAtNormalRates + r.TaxAtSpecialRates).Should().Be(r.TaxBeforeRebate,
            "tax at normal + special rates must reconcile to the tax before rebate");
    }

    [Fact]
    public void A_plain_salaried_return_has_an_empty_special_income_block()
    {
        var r = _engine.Compute(RuleSetFixture.Salaried(1_000_000m), Regime.New);

        r.SpecialIncome.Total.Should().Be(0m);
        r.SpecialIncome.SlabRateCapitalGains.Should().Be(0m);
        r.TaxAtSpecialRates.Should().Be(0m);
        r.TaxAtNormalRates.Should().Be(r.TaxBeforeRebate);
    }
}
