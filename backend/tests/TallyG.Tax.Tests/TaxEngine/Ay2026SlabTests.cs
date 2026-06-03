using FluentAssertions;
using TallyG.Tax.Domain.Enums;
using TallyG.Tax.Domain.TaxEngine;
using Xunit;

namespace TallyG.Tax.Tests.TaxEngine;

/// <summary>
/// Golden tests for the AY2026-27 (Budget 2025) new-regime slab changes — hand-computed to the rupee.
///
/// New slabs: ₹0–4L @0%, 4–8L @5%, 8–12L @10%, 12–16L @15%, 16–20L @20%, 20–24L @25%, >24L @30%.
/// 87A: threshold ₹12,00,000, max rebate ₹60,000 (marginal relief). Cess 4%.
/// Standard deduction ₹75,000. Income rounding: nearest ₹10.
/// </summary>
public class Ay2026SlabTests
{
    private readonly ITaxCalculator _eng = new TaxCalculator();

    // ---- Key: ₹12L salary → ₹0 tax (the Budget 2025 main benefit) ----

    [Fact]
    public void Salary_of_12L_is_effectively_nil_tax_due_to_87A_rebate()
    {
        // Taxable = ₹12L − ₹75k std ded = ₹11.25L (rounded ₹10 = ₹11.25L)
        // Slabs: 0 + 5%×₹4L + 10%×₹3.25L = 0 + ₹20,000 + ₹32,500 = ₹52,500
        // 87A: income ₹11.25L ≤ ₹12L → rebate = min(₹52,500, ₹60,000) = ₹52,500 → tax = 0
        var r = _eng.Compute(RuleSetFixture.Salaried2026(1_200_000m), Regime.New);

        r.TaxableIncome.Should().Be(1_125_000m);
        r.Rebate87A.Should().Be(52_500m);
        r.TotalTax.Should().Be(0m, "₹12L salary → nil tax under Budget 2025 new regime (87A rebate wipes the slab)");
    }

    [Fact]
    public void Salary_just_above_12L_crosses_into_taxable_territory()
    {
        // Gross ₹13L − ₹75k = ₹12.25L taxable.
        // Slabs: 0 + ₹20,000 + ₹40,000 + 15%×₹0.25L = ₹3,750 → ₹63,750
        // 87A: income ₹12.25L > ₹12L threshold → NO rebate.
        // Marginal relief: excessIncome = 12.25L − 12L = 25,000; tax ₹63,750 > ₹25,000 →
        //   relief = ₹63,750 − ₹25,000 = ₹38,750; effective tax = ₹25,000 + 4% cess = ₹26,000.
        var r = _eng.Compute(RuleSetFixture.Salaried2026(1_300_000m), Regime.New);

        r.TaxableIncome.Should().Be(1_225_000m);
        // With marginal relief the effective net payable should equal the excess over ₹12L (₹25k + cess).
        r.TotalTax.Should().Be(26_000m, "marginal relief ensures tax ≤ income excess over the ₹12L threshold");
    }

    [Fact]
    public void Salary_of_18L_uses_the_upper_slabs_correctly()
    {
        // Gross ₹18L − ₹75k = ₹17.25L taxable.
        // Slabs: 0 + ₹20,000 + ₹40,000 + 15%×₹4L=₹60,000 + 20%×₹1.25L=₹25,000 = ₹1,45,000
        // No 87A (income > ₹12L, and marginal relief doesn't apply here since slab > excess).
        // Cess: ₹1,45,000 × 1.04 = ₹1,50,800.
        var r = _eng.Compute(RuleSetFixture.Salaried2026(1_800_000m), Regime.New);

        r.TaxableIncome.Should().Be(1_725_000m);
        r.TotalTax.Should().Be(150_800m, "₹18L gross / ₹17.25L taxable: slabs + 4% cess = ₹1,50,800 (AY2026-27 new regime)");
    }

    [Fact]
    public void Ay2026_27_old_regime_87A_threshold_stays_at_5L()
    {
        // Old regime's 87A threshold has not changed (₹5L, ₹12,500 max). The new regime change
        // should NOT leak into the old-regime computation.
        var r = _eng.Compute(RuleSetFixture.Salaried2026(600_000m), Regime.Old);

        // Old regime: ₹6L gross − ₹75k std = ₹5.25L taxable.
        // Slabs: nil (≤₹2.5L) + 5%×₹2.5L (=₹12,500) + 20%×₹0.25L (=₹5,000) = ₹17,500. Income ₹5.25L > ₹5L → no rebate.
        // + 4% cess = ₹18,200.
        r.TaxableIncome.Should().Be(525_000m);
        r.Rebate87A.Should().Be(0m, "old-regime 87A threshold is ₹5L — ₹5.25L does not qualify");
        r.TotalTax.Should().Be(18_200m);
    }
}
