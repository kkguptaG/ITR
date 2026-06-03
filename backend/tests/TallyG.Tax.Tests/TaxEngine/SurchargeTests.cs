using FluentAssertions;
using TallyG.Tax.Domain.Enums;
using TallyG.Tax.Domain.TaxEngine;
using Xunit;

namespace TallyG.Tax.Tests.TaxEngine;

/// <summary>
/// Golden tests for the banded surcharge (s.2 Finance Act): the 15% CAP on the surcharge attributable to
/// 111A/112A/112 special-rate income, and marginal relief at a band edge. Expected values are computed by
/// hand from the AY2025-26 rule-set (RuleSetFixture) so a regression in the surcharge math fails the test.
/// </summary>
public class SurchargeTests
{
    private readonly ITaxCalculator _engine = new TaxCalculator();

    private static TaxComputationInput Base() => new()
    {
        AssessmentYearCode = "AY2025-26",
        RuleSetVersion = RuleSetFixture.Version,
        RulesJson = RuleSetFixture.Ay2025_26Json,
        Age = 35,
    };

    private static TaxComputationInput Business(decimal netProfit)
        => Base() with { BusinessIncomes = new[] { new BusinessIncomeInput(false, null, 0m, 0m, 0m, netProfit, false) } };

    [Fact]
    public void New_regime_surcharge_marginal_relief_at_the_50L_band_edge()
    {
        // Business income ₹51L (new regime, no deductions). Slab tax: ₹11.9L at ₹50L, ₹12.2L at ₹51L.
        // Band >₹50L = 10% ⇒ surcharge ₹1,22,000 before relief. Marginal relief caps (tax + surcharge) at
        // (tax at ₹50L) + (income over ₹50L): ₹11,90,000 + ₹1,00,000 = ₹12,90,000, so surcharge is reduced
        // by ₹52,000 to ₹70,000.
        var r = _engine.Compute(Business(5_100_000m), Regime.New);

        r.Surcharge.Should().Be(70_000m);
        r.Trace.Should().Contain(t => t.Step == "Surcharge.MarginalRelief");
    }

    [Fact]
    public void Surcharge_on_112A_long_term_gain_is_capped_at_15_percent_not_the_band_rate()
    {
        // ₹3Cr of 112A LTCG, no other income. Taxable LTCG = ₹3Cr − ₹1.25L exemption = ₹2,98,75,000;
        // tax @ 12.5% = ₹37,34,375. Total income > ₹2Cr ⇒ the band rate is 25%, BUT the surcharge on
        // special-rate income is capped at 15%. So surcharge = 15% × ₹37,34,375, NOT 25%.
        var ltcg = new CapitalGainInput(
            CapitalGainAssetType.ListedEquity, CapitalGainTerm.Long, "112A",
            SaleConsideration: 30_000_000m, CostOfAcquisition: 0m, CostOfImprovement: 0m,
            ExpensesOnTransfer: 0m, ExemptionAmount: 0m, AcquisitionDate: null, TransferDate: null);
        var r = _engine.Compute(Base() with { CapitalGains = new[] { ltcg } }, Regime.New);

        var ltcgTax = (30_000_000m - 125_000m) * 0.125m;   // taxable gain × s.112A rate
        r.Surcharge.Should().BeApproximately(ltcgTax * 0.15m, 1m, "112A surcharge is capped at 15%");
        r.Surcharge.Should().BeLessThan(ltcgTax * 0.25m, "the 25% band rate must NOT apply to special-rate income");
        r.Trace.Should().Contain(t => t.Step == "Surcharge" && t.Description.Contains("capped"));
    }

    [Fact]
    public void All_normal_income_above_2Cr_takes_the_full_band_rate()
    {
        // ₹3Cr business income, all normal (no special-rate income) ⇒ surcharge is the full 25% band rate
        // and STRICTLY MORE than the same total income realised as capped special-rate gains.
        var allNormal = _engine.Compute(Business(30_000_000m), Regime.New);

        var asLtcg = _engine.Compute(
            Base() with
            {
                CapitalGains = new[]
                {
                    new CapitalGainInput(CapitalGainAssetType.ListedEquity, CapitalGainTerm.Long, "112A",
                        30_000_000m, 0m, 0m, 0m, 0m, null, null),
                },
            },
            Regime.New);

        allNormal.Surcharge.Should().BeGreaterThan(asLtcg.Surcharge,
            "the same ₹3Cr taxed as normal income bears 25% surcharge vs the 15%-capped special-rate gain");
    }
}
