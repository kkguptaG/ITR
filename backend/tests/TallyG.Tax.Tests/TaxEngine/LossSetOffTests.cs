using FluentAssertions;
using TallyG.Tax.Domain.Enums;
using TallyG.Tax.Domain.TaxEngine;
using Xunit;

namespace TallyG.Tax.Tests.TaxEngine;

/// <summary>
/// Brought-forward (earlier-year) loss set-off: a carried-forward loss absorbs only the SAME head's
/// current-year income, and the unused part keeps carrying forward. AY2025-26 fixture, NEW regime.
/// </summary>
public class LossSetOffTests
{
    private readonly ITaxCalculator _engine = new TaxCalculator();

    private static TaxComputationInput Build(
        decimal bfHpLoss = 0m,
        decimal bfBusinessLoss = 0m,
        HousePropertyInput[]? houses = null,
        BusinessIncomeInput[]? businesses = null)
        => new()
        {
            AssessmentYearCode = "AY2025-26",
            RuleSetVersion = "1.0.0",
            RulesJson = RuleSetFixture.Ay2025_26Json,
            Age = 35,
            HouseProperties = houses ?? System.Array.Empty<HousePropertyInput>(),
            BusinessIncomes = businesses ?? System.Array.Empty<BusinessIncomeInput>(),
            BroughtForwardHousePropertyLoss = bfHpLoss,
            BroughtForwardBusinessLoss = bfBusinessLoss,
        };

    [Fact]
    public void Brought_forward_house_property_loss_sets_off_against_current_HP_income()
    {
        // Let-out NAV 5,00,000 → 30% std → ₹3,50,000 HP income; ₹2,00,000 b/f HP loss absorbs part.
        var r = _engine.Compute(
            Build(bfHpLoss: 200_000m, houses: new[] { new HousePropertyInput(HousePropertyType.LetOut, 500_000m, 0m, 0m) }),
            Regime.New);

        r.GrossTotalIncome.Should().Be(150_000m); // 3,50,000 − 2,00,000
        r.Trace.Should().Contain(t => t.Step == "HouseProperty.BfLossSetOff" && t.Amount == 200_000m);
    }

    [Fact]
    public void Brought_forward_business_loss_sets_off_and_remainder_carries_forward()
    {
        // ₹3,00,000 business profit; ₹5,00,000 b/f loss → absorbs 3,00,000, carries 2,00,000 forward.
        var r = _engine.Compute(
            Build(bfBusinessLoss: 500_000m,
                  businesses: new[] { new BusinessIncomeInput(false, null, 0m, 0m, 0m, 300_000m, false) }),
            Regime.New);

        r.GrossTotalIncome.Should().Be(0m);
        r.Trace.Should().Contain(t => t.Step == "Business.BfLossSetOff" && t.Amount == 300_000m);
        r.Trace.Should().Contain(t => t.Step == "Business.BfLossCarryForward" && t.Amount == 200_000m);
    }

    [Fact]
    public void Brought_forward_loss_cannot_cross_heads()
    {
        // A b/f HP loss must NOT touch business income (and vice-versa).
        var r = _engine.Compute(
            Build(bfHpLoss: 400_000m,
                  businesses: new[] { new BusinessIncomeInput(false, null, 0m, 0m, 0m, 300_000m, false) }),
            Regime.New);

        r.GrossTotalIncome.Should().Be(300_000m); // business income untouched by the HP loss
        r.Trace.Should().Contain(t => t.Step == "HouseProperty.BfLossCarryForward" && t.Amount == 400_000m);
    }
}
