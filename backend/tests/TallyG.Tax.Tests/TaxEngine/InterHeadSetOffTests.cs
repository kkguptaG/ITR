using System;
using FluentAssertions;
using TallyG.Tax.Domain.Enums;
using TallyG.Tax.Domain.TaxEngine;
using Xunit;

namespace TallyG.Tax.Tests.TaxEngine;

/// <summary>
/// Current-year INTER-HEAD loss set-off (s.71) and carry-forward (s.71B/72/73). Distinct from the
/// brought-forward (earlier-year) set-off in <see cref="LossSetOffTests"/>: here the loss arises THIS
/// year. AY2025-26 fixture, NEW regime, age 35 (new slabs: 0/5/10/15/20/30%, 87A ≤ ₹7L).
/// </summary>
public class InterHeadSetOffTests
{
    private readonly ITaxCalculator _engine = new TaxCalculator();

    private static TaxComputationInput Build(
        SalaryInput[]? salaries = null,
        HousePropertyInput[]? houses = null,
        BusinessIncomeInput[]? businesses = null,
        CapitalGainInput[]? capitalGains = null,
        OtherIncomeInput[]? others = null)
        => new()
        {
            AssessmentYearCode = "AY2025-26",
            RuleSetVersion = "1.0.0",
            RulesJson = RuleSetFixture.Ay2025_26Json,
            Age = 35,
            Salaries = salaries ?? Array.Empty<SalaryInput>(),
            HouseProperties = houses ?? Array.Empty<HousePropertyInput>(),
            BusinessIncomes = businesses ?? Array.Empty<BusinessIncomeInput>(),
            CapitalGains = capitalGains ?? Array.Empty<CapitalGainInput>(),
            OtherIncomes = others ?? Array.Empty<OtherIncomeInput>(),
        };

    private static SalaryInput Salary(decimal gross) => new("Acme", gross, 0m, 0m, 0m, 0m);
    private static BusinessIncomeInput Business(decimal netProfit, bool speculative = false)
        => new(false, null, 0m, 0m, 0m, netProfit, speculative);

    [Fact]
    public void Business_loss_sets_off_against_house_property_income()
    {
        // Let-out HP income ₹3,50,000 (NAV 5L − 30%); non-speculative business loss ₹1,50,000.
        // s.71: business loss may reduce HP income → GTI ₹2,00,000; nothing carries forward.
        var r = _engine.Compute(
            Build(houses: new[] { new HousePropertyInput(HousePropertyType.LetOut, 500_000m, 0m, 0m) },
                  businesses: new[] { Business(-150_000m) }),
            Regime.New);

        r.GrossTotalIncome.Should().Be(200_000m);
        r.BusinessLossCarriedForward.Should().Be(0m);
        r.Trace.Should().Contain(t => t.Step == "SetOff.Business" && t.Amount == 150_000m);
    }

    [Fact]
    public void Business_loss_cannot_set_off_against_salary_and_carries_forward_under_s72()
    {
        // Salary ₹10,00,000 (net ₹9,25,000) + business loss ₹3,00,000. s.71(2A): a business loss may
        // NOT touch salary → salary taxed in full, the whole loss carries forward u/s 72.
        var r = _engine.Compute(
            Build(salaries: new[] { Salary(1_000_000m) }, businesses: new[] { Business(-300_000m) }),
            Regime.New);

        r.GrossTotalIncome.Should().Be(925_000m);                 // salary untouched by the business loss
        r.BusinessLossCarriedForward.Should().Be(300_000m);
        r.TotalTax.Should().Be(44_200m);                          // 20,000 + 22,500 = 42,500 + 4% cess
        r.Trace.Should().Contain(t => t.Step == "CarryForward.Business" && t.Amount == 300_000m);
        r.Trace.Should().NotContain(t => t.Step == "SetOff.Business");
    }

    [Fact]
    public void House_property_loss_inter_head_set_off_is_capped_at_2L_and_balance_carries_under_s71B()
    {
        // Let-out interest ₹5,00,000 (NAV 0) → HP loss ₹5,00,000; salary ₹12,00,000 (net ₹11,25,000).
        // s.71(3A): only ₹2,00,000 sets off against salary this year; ₹3,00,000 carries forward (s.71B).
        var r = _engine.Compute(
            Build(salaries: new[] { Salary(1_200_000m) },
                  houses: new[] { new HousePropertyInput(HousePropertyType.LetOut, 0m, 0m, 500_000m) }),
            Regime.New);

        r.GrossTotalIncome.Should().Be(925_000m);                 // 11,25,000 − 2,00,000 capped set-off
        r.HousePropertyLossCarriedForward.Should().Be(300_000m);
        r.TotalTax.Should().Be(44_200m);
        r.Trace.Should().Contain(t => t.Step == "SetOff.HouseProperty" && t.Amount == 200_000m);
        r.Trace.Should().Contain(t => t.Step == "CarryForward.HouseProperty" && t.Amount == 300_000m);
    }

    [Fact]
    public void Business_loss_sets_off_against_capital_gains_special_rate_bucket()
    {
        // No normal income; LTCG s.112 ₹4,00,000 (unlisted, 12.5%) + business loss ₹2,00,000.
        // s.71 reaches the special-rate bucket → only ₹2,00,000 LTCG remains taxable.
        var r = _engine.Compute(
            Build(businesses: new[] { Business(-200_000m) },
                  capitalGains: new[]
                  {
                      new CapitalGainInput(CapitalGainAssetType.UnlistedShares, CapitalGainTerm.Long, null,
                          500_000m, 100_000m, 0m, 0m, 0m, null, null),
                  }),
            Regime.New);

        r.TotalTax.Should().Be(26_000m);                          // 2,00,000 × 12.5% = 25,000 + 4% cess
        r.BusinessLossCarriedForward.Should().Be(0m);
        r.Trace.Should().Contain(t => t.Step == "SetOff.Business" && t.Amount == 200_000m);
    }

    [Fact]
    public void Speculative_loss_is_ring_fenced_and_carries_forward_under_s73()
    {
        // Non-speculative business profit ₹3,00,000 + speculative loss ₹1,00,000. s.73: the speculative
        // loss must NOT reduce the non-speculative profit → GTI ₹3,00,000, ₹1,00,000 carries forward.
        var r = _engine.Compute(
            Build(businesses: new[] { Business(300_000m), Business(-100_000m, speculative: true) }),
            Regime.New);

        r.GrossTotalIncome.Should().Be(300_000m);
        r.SpeculativeLossCarriedForward.Should().Be(100_000m);
        r.BusinessLossCarriedForward.Should().Be(0m);
        r.Trace.Should().Contain(t => t.Step == "CarryForward.Speculative" && t.Amount == 100_000m);
    }

    [Fact]
    public void Non_speculative_loss_may_be_absorbed_by_speculative_profit_within_the_head()
    {
        // Non-speculative loss ₹2,00,000 + speculative profit ₹3,00,000. s.70: a non-speculative loss
        // CAN be set off against speculative profit (same head) → net business income ₹1,00,000.
        var r = _engine.Compute(
            Build(businesses: new[] { Business(-200_000m), Business(300_000m, speculative: true) }),
            Regime.New);

        r.GrossTotalIncome.Should().Be(100_000m);
        r.BusinessLossCarriedForward.Should().Be(0m);
        r.SpeculativeLossCarriedForward.Should().Be(0m);
    }

    [Fact]
    public void No_current_year_loss_is_a_no_op()
    {
        // Regression guard: with no losses the engine is byte-for-byte unchanged. Salary ₹15,00,000.
        var r = _engine.Compute(Build(salaries: new[] { Salary(1_500_000m) }), Regime.New);

        r.TotalTax.Should().Be(130_000m);                         // 1,25,000 slab + 4% cess
        r.HousePropertyLossCarriedForward.Should().Be(0m);
        r.BusinessLossCarriedForward.Should().Be(0m);
        r.SpeculativeLossCarriedForward.Should().Be(0m);
    }
}
