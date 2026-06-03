using FluentAssertions;
using TallyG.Tax.Domain.Enums;
using TallyG.Tax.Domain.TaxEngine;
using Xunit;

namespace TallyG.Tax.Tests.TaxEngine;

/// <summary>
/// Income-from-Other-Sources segregation: winnings/casual income at the s.115BB flat rate, and
/// agricultural-income partial integration. Hand-computed against the AY2025-26 fixture
/// (OLD basic exemption ₹2.5L; cess 4%).
/// </summary>
public class OtherSourcesTaxTests
{
    private readonly ITaxCalculator _engine = new TaxCalculator();

    private static TaxComputationInput Build(decimal salaryGross, params OtherIncomeInput[] others)
        => new()
        {
            AssessmentYearCode = "AY2025-26",
            RuleSetVersion = "1.0.0",
            RulesJson = RuleSetFixture.Ay2025_26Json,
            Age = 35,
            Salaries = salaryGross > 0m
                ? new[] { new SalaryInput("Acme", salaryGross, 0m, 0m, 0m, 0m) }
                : System.Array.Empty<SalaryInput>(),
            OtherIncomes = others,
        };

    [Fact]
    public void Lottery_is_taxed_flat_30pct_under_115BB()
    {
        var r = _engine.Compute(Build(0m, new OtherIncomeInput("Game show", 1_000_000m, "lottery_115bb")), Regime.New);

        r.GrossTotalIncome.Should().Be(1_000_000m);
        r.Rebate87A.Should().Be(0m);          // s.87A never applies to 115BB income
        r.TotalTax.Should().Be(312_000m);     // 30% = ₹3,00,000 + 4% cess
        r.Trace.Should().Contain(t => t.Step == "Tax.Casual115BB" && t.Amount == 300_000m);
    }

    [Fact]
    public void Online_game_winnings_are_taxed_flat_30pct_under_115BBJ()
    {
        // Winnings from online real-money games (s.115BBJ) share the flat 30% casual rate, so the engine pools
        // them with s.115BB winnings (the section split is only a Schedule OS / SI disclosure matter).
        var r = _engine.Compute(Build(0m, new OtherIncomeInput("Online rummy", 1_000_000m, "online_gaming_115bbj")), Regime.New);

        r.GrossTotalIncome.Should().Be(1_000_000m);
        r.Rebate87A.Should().Be(0m);          // s.87A never applies to flat-rate winnings
        r.TotalTax.Should().Be(312_000m);     // 30% = ₹3,00,000 + 4% cess
        r.Trace.Should().Contain(t => t.Step == "Tax.Casual115BB" && t.Amount == 300_000m);
    }

    [Fact]
    public void Agricultural_income_partial_integration_raises_the_rate()
    {
        var r = _engine.Compute(Build(1_000_000m, new OtherIncomeInput("Farm", 500_000m, "agricultural")), Regime.Old);

        // slabTax(9.5L + 5L) − slabTax(5L + 2.5L) = 2,47,500 − 62,500 = 1,85,000; + 4% cess.
        r.TotalTax.Should().Be(192_400m);
        r.GrossTotalIncome.Should().Be(950_000m); // agri is exempt — not in GTI
        r.Trace.Should().Contain(t => t.Step == "RebateOnAgriculturalIncome" && t.Amount == 62_500m);
    }

    [Fact]
    public void Small_agricultural_income_below_threshold_is_ignored()
    {
        var r = _engine.Compute(Build(1_000_000m, new OtherIncomeInput("Farm", 4_000m, "agricultural")), Regime.Old);

        r.TotalTax.Should().Be(106_600m); // slabTax(9.5L) = 1,02,500 + 4% cess; no integration
        r.Trace.Should().NotContain(t => t.Step == "RebateOnAgriculturalIncome");
    }
}
