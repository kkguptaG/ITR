using FluentAssertions;
using TallyG.Tax.Domain.Enums;
using TallyG.Tax.Domain.TaxEngine;
using Xunit;

namespace TallyG.Tax.Tests.TaxEngine;

/// <summary>
/// Regime-difference golden cases: HRA exemption (s.10(13A)) and self-occupied house-property interest
/// (s.24(b)) are OLD-regime only and capped, and must be ignored under the new regime.
/// </summary>
public class SalaryHraAndHousePropertyTests
{
    private readonly ITaxCalculator _engine = new TaxCalculator();

    private static TaxComputationInput WithHouse(decimal salaryGross, HousePropertyInput house) => new()
    {
        AssessmentYearCode = "AY2025-26",
        RuleSetVersion = "1.0.0",
        RulesJson = RuleSetFixture.Ay2025_26Json,
        Age = 35,
        Salaries = new[] { new SalaryInput("Acme", salaryGross, 0m, 0m, 0m, 0m) },
        HouseProperties = new[] { house },
    };

    [Fact]
    public void Hra_exemption_reduces_taxable_income_under_old_but_is_ignored_under_new()
    {
        var input = RuleSetFixture.Salaried(1_200_000m, hraExemption: 200_000m);

        var old = _engine.Compute(input, Regime.Old);
        var neu = _engine.Compute(input, Regime.New);

        old.TaxableIncome.Should().Be(950_000m);   // 12L − 2L HRA − 50k std
        old.TotalTax.Should().Be(106_600m);         // slab 1,02,500 + 4% cess
        neu.TaxableIncome.Should().Be(1_125_000m);  // 12L − 75k std (HRA disallowed)
    }

    [Fact]
    public void Self_occupied_interest_loss_is_capped_at_2L_old_and_disallowed_new()
    {
        var house = new HousePropertyInput(HousePropertyType.SelfOccupied, AnnualValue: 0m, MunicipalTaxesPaid: 0m, InterestOnLoan: 300_000m);

        var old = _engine.Compute(WithHouse(1_500_000m, house), Regime.Old);
        var neu = _engine.Compute(WithHouse(1_500_000m, house), Regime.New);

        // Old: salary 14.5L net − ₹2L capped self-occupied loss (₹3L interest capped) = ₹12.5L.
        old.TaxableIncome.Should().Be(1_250_000m);
        // New: 24(b) self-occupied interest disallowed ⇒ no loss; 15L − 75k std = 14.25L.
        neu.TaxableIncome.Should().Be(1_425_000m);
    }
}
