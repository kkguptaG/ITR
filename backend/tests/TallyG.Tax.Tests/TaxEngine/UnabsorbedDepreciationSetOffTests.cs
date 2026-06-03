using FluentAssertions;
using TallyG.Tax.Domain.Enums;
using TallyG.Tax.Domain.TaxEngine;
using Xunit;

namespace TallyG.Tax.Tests.TaxEngine;

/// <summary>
/// Locks the s.32(2) brought-forward unabsorbed-depreciation set-off. Unlike a business loss it sets off
/// against income under ANY head EXCEPT salary, and the unused balance carries forward indefinitely. The
/// tests are delta-based (with-vs-without the b/f UD) so they don't depend on slab/deduction arithmetic.
/// </summary>
public class UnabsorbedDepreciationSetOffTests
{
    private readonly ITaxCalculator _engine = new TaxCalculator();

    private static TaxComputationInput Build(
        decimal broughtForwardUd, decimal businessNetProfit = 0m, decimal otherIncome = 0m, decimal salaryGross = 0m)
        => new()
        {
            AssessmentYearCode = "AY2025-26",
            RuleSetVersion = RuleSetFixture.Version,
            RulesJson = RuleSetFixture.Ay2025_26Json,
            Age = 35,
            Salaries = salaryGross > 0m
                ? new[] { new SalaryInput("Acme Corp", salaryGross, 0m, 0m, 0m, 0m) }
                : Array.Empty<SalaryInput>(),
            BusinessIncomes = businessNetProfit != 0m
                ? new[] { new BusinessIncomeInput(false, null, 0m, 0m, 0m, businessNetProfit, false) }
                : Array.Empty<BusinessIncomeInput>(),
            OtherIncomes = otherIncome > 0m
                ? new[] { new OtherIncomeInput("Interest", otherIncome, "interest") }
                : Array.Empty<OtherIncomeInput>(),
            BroughtForwardUnabsorbedDepreciation = broughtForwardUd,
        };

    [Fact]
    public void Sets_off_against_business_income_reducing_gross_total_income()
    {
        var withUd = _engine.Compute(Build(broughtForwardUd: 200_000m, businessNetProfit: 500_000m), Regime.New);
        var withoutUd = _engine.Compute(Build(broughtForwardUd: 0m, businessNetProfit: 500_000m), Regime.New);

        withUd.GrossTotalIncome.Should().Be(withoutUd.GrossTotalIncome - 200_000m);
        withUd.UnabsorbedDepreciationCarriedForward.Should().Be(0m, "the ₹2L b/f UD is fully absorbed by ₹5L business income");
        withUd.Trace.Should().Contain(t => t.Step == "UnabsorbedDepreciation.SetOff" && t.Amount == 200_000m);
    }

    [Fact]
    public void Sets_off_against_other_sources_income_too()
    {
        var withUd = _engine.Compute(Build(broughtForwardUd: 150_000m, otherIncome: 400_000m), Regime.New);
        var withoutUd = _engine.Compute(Build(broughtForwardUd: 0m, otherIncome: 400_000m), Regime.New);

        withUd.GrossTotalIncome.Should().Be(withoutUd.GrossTotalIncome - 150_000m, "UD sets off against any head except salary");
        withUd.UnabsorbedDepreciationCarriedForward.Should().Be(0m);
    }

    [Fact]
    public void Cannot_set_off_against_salary_so_it_carries_forward()
    {
        var withUd = _engine.Compute(Build(broughtForwardUd: 200_000m, salaryGross: 800_000m), Regime.New);
        var withoutUd = _engine.Compute(Build(broughtForwardUd: 0m, salaryGross: 800_000m), Regime.New);

        withUd.GrossTotalIncome.Should().Be(withoutUd.GrossTotalIncome, "s.32(2) UD cannot be set off against salary income");
        withUd.UnabsorbedDepreciationCarriedForward.Should().Be(200_000m, "with no non-salary income, the whole b/f UD carries forward");
    }

    [Fact]
    public void Absorbs_only_up_to_available_income_and_carries_the_rest_forward()
    {
        var withUd = _engine.Compute(Build(broughtForwardUd: 300_000m, businessNetProfit: 100_000m), Regime.New);
        var withoutUd = _engine.Compute(Build(broughtForwardUd: 0m, businessNetProfit: 100_000m), Regime.New);

        // Only ₹1L of the ₹3L UD is absorbed (capped at the business income); ₹2L carries forward.
        withUd.GrossTotalIncome.Should().Be(withoutUd.GrossTotalIncome - 100_000m);
        withUd.UnabsorbedDepreciationCarriedForward.Should().Be(200_000m);
    }
}
