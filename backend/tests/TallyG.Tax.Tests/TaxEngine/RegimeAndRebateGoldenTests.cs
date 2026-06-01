using FluentAssertions;
using TallyG.Tax.Domain.Enums;
using TallyG.Tax.Domain.TaxEngine;
using Xunit;

namespace TallyG.Tax.Tests.TaxEngine;

/// <summary>
/// Golden cases for the s.87A rebate (both regimes) and the old-vs-new recommendation, against the
/// AY2025-26 fixture (old: rebate ≤ ₹5L / ₹12,500; new: rebate ≤ ₹7L / ₹25,000; std ded ₹50k old, ₹75k new).
/// </summary>
public class RegimeAndRebateGoldenTests
{
    private readonly ITaxCalculator _engine = new TaxCalculator();

    [Fact]
    public void Old_regime_87A_makes_tax_nil_up_to_5_lakh_total_income()
    {
        // ₹5.5L salary − ₹50k std = ₹5L; the ₹12,500 slab tax is fully rebated u/s 87A.
        var r = _engine.Compute(RuleSetFixture.Salaried(550_000m), Regime.Old);

        r.TaxableIncome.Should().Be(500_000m);
        r.TotalTax.Should().Be(0m);
    }

    [Fact]
    public void New_regime_87A_makes_tax_nil_up_to_7_lakh_total_income()
    {
        // ₹7.5L salary − ₹75k std = ₹6.75L; the ₹18,750 slab tax is fully rebated u/s 87A.
        var r = _engine.Compute(RuleSetFixture.Salaried(750_000m), Regime.New);

        r.TaxableIncome.Should().Be(675_000m);
        r.TotalTax.Should().Be(0m);
    }

    [Fact]
    public void Regime_compare_recommends_new_for_a_deduction_free_15L_salary()
    {
        var cmp = _engine.Compare(RuleSetFixture.Salaried(1_500_000m));

        cmp.Old.TotalTax.Should().Be(257_400m);   // 2,47,500 slab + 4% cess
        cmp.New.TotalTax.Should().Be(130_000m);   // 1,25,000 slab + 4% cess
        cmp.Recommended.Should().Be(Regime.New);
        cmp.SavingsVsAlternative.Should().Be(127_400m);
    }
}
