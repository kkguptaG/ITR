using FluentAssertions;
using TallyG.Tax.Domain.Enums;
using TallyG.Tax.Domain.TaxEngine;
using Xunit;

namespace TallyG.Tax.Tests.TaxEngine;

/// <summary>
/// Chapter VI-A depth for the disability / medical / loan-interest sections: 80U and 80DD are FIXED
/// deductions (₹75k, or ₹1.25L "severe"); 80DDB is least-of-spend-and-cap (₹40k, ₹1L for seniors);
/// 80EEA / 80EEB cap loan interest at ₹1.5L. All are OLD-regime only (disallowed under 115BAC).
/// Computed against the AY2025-26 fixture via the shared <see cref="RuleSetFixture.Salaried"/> helper.
/// </summary>
public class DeductionDepthTests
{
    private readonly ITaxCalculator _engine = new TaxCalculator();

    private static DeductionInput[] D(string section, decimal amount, string? subType = null)
        => new[] { new DeductionInput(section, amount, subType) };

    [Fact]
    public void Section80U_is_a_fixed_deduction_independent_of_the_amount_entered()
    {
        var r = _engine.Compute(RuleSetFixture.Salaried(1_000_000m, deductions: D("80U", 10_000m)), Regime.Old);

        // ₹10k entered, but the statutory fixed deduction is ₹75,000.
        r.Trace.Should().Contain(t => t.Step == "Deduction.80U" && t.Amount == 75_000m);
        r.TaxableIncome.Should().Be(875_000m); // 10,00,000 − 50,000 std − 75,000
    }

    [Fact]
    public void Section80U_severe_disability_is_the_higher_fixed_amount()
    {
        var r = _engine.Compute(RuleSetFixture.Salaried(1_000_000m, deductions: D("80U", 0m, "severe")), Regime.Old);

        r.Trace.Should().Contain(t => t.Step == "Deduction.80U" && t.Amount == 125_000m);
    }

    [Fact]
    public void Section80DD_dependent_disability_is_fixed_too()
    {
        var r = _engine.Compute(RuleSetFixture.Salaried(1_000_000m, deductions: D("80DD", 200_000m)), Regime.Old);

        r.Trace.Should().Contain(t => t.Step == "Deduction.80DD" && t.Amount == 75_000m);
    }

    [Fact]
    public void Section80DDB_is_capped_at_40k_below_60()
    {
        var r = _engine.Compute(RuleSetFixture.Salaried(1_000_000m, deductions: D("80DDB", 60_000m), age: 40), Regime.Old);

        r.Trace.Should().Contain(t => t.Step == "Deduction.80DDB" && t.Amount == 40_000m);
    }

    [Fact]
    public void Section80DDB_cap_rises_to_1L_for_seniors()
    {
        var r = _engine.Compute(RuleSetFixture.Salaried(1_000_000m, deductions: D("80DDB", 60_000m), age: 65), Regime.Old);

        // Senior cap is ₹1L, so the full ₹60k spend is allowed (a below-60 filer would be capped at ₹40k).
        r.Trace.Should().Contain(t => t.Step == "Deduction.80DDB" && t.Amount == 60_000m);
    }

    [Fact]
    public void Section80EEB_ev_loan_interest_is_capped_at_150k()
    {
        var r = _engine.Compute(RuleSetFixture.Salaried(2_000_000m, deductions: D("80EEB", 200_000m)), Regime.Old);

        r.Trace.Should().Contain(t => t.Step == "Deduction.80EEB" && t.Amount == 150_000m);
    }

    [Fact]
    public void Section80EEA_within_cap_is_allowed_in_full()
    {
        var r = _engine.Compute(RuleSetFixture.Salaried(2_000_000m, deductions: D("80EEA", 100_000m)), Regime.Old);

        r.Trace.Should().Contain(t => t.Step == "Deduction.80EEA" && t.Amount == 100_000m);
    }

    [Fact]
    public void Hyphenated_section_labels_are_recognised()
    {
        var r = _engine.Compute(RuleSetFixture.Salaried(2_000_000m, deductions: D("80-EEB", 200_000m)), Regime.Old);

        r.Trace.Should().Contain(t => t.Step == "Deduction.80EEB" && t.Amount == 150_000m);
    }

    [Fact]
    public void Disability_deductions_are_disallowed_under_the_new_regime()
    {
        var r = _engine.Compute(RuleSetFixture.Salaried(1_000_000m, deductions: D("80U", 0m, "severe")), Regime.New);

        r.Trace.Should().NotContain(t => t.Step == "Deduction.80U");
    }

    [Fact]
    public void Section80GG_rent_relief_is_capped_at_60k_per_year()
    {
        // Salary ₹8L − ₹50k = ₹7.5L income; rent ₹1.8L. Least of ₹60k / 25%×7.5L=₹1.875L /
        // (1.8L − 10%×7.5L = ₹1.05L) ⇒ the ₹60k annual cap binds.
        var r = _engine.Compute(RuleSetFixture.Salaried(800_000m, deductions: D("80GG", 180_000m)), Regime.Old);

        r.Trace.Should().Contain(t => t.Step == "Deduction.80GG" && t.Amount == 60_000m);
        r.TaxableIncome.Should().Be(690_000m); // 7,50,000 − 60,000
    }

    [Fact]
    public void Section80GG_rent_minus_ten_percent_arm_can_bind()
    {
        // Rent ₹1.2L, income ₹7.5L ⇒ rent − 10% income = 1.2L − 75k = ₹45k, below the ₹60k cap and 25% arm.
        var r = _engine.Compute(RuleSetFixture.Salaried(800_000m, deductions: D("80GG", 120_000m)), Regime.Old);

        r.Trace.Should().Contain(t => t.Step == "Deduction.80GG" && t.Amount == 45_000m);
    }

    [Fact]
    public void Section80GG_is_disallowed_under_the_new_regime()
    {
        var r = _engine.Compute(RuleSetFixture.Salaried(800_000m, deductions: D("80GG", 180_000m)), Regime.New);

        r.Trace.Should().NotContain(t => t.Step == "Deduction.80GG");
    }

    [Fact]
    public void Section80G_100pct_no_limit_donation_is_fully_deductible()
    {
        var r = _engine.Compute(RuleSetFixture.Salaried(1_000_000m, deductions: D("80G", 50_000m, "100_no_limit")), Regime.Old);

        r.Trace.Should().Contain(t => t.Step == "Deduction.80G" && t.Amount == 50_000m);
    }

    [Fact]
    public void Section80G_with_limit_is_capped_at_10pct_of_adjusted_gti_then_halved()
    {
        // Income ₹9.5L ⇒ qualifying limit 10% = ₹95,000. A ₹3L "50% with-limit" donation is capped to
        // ₹95k, then halved ⇒ ₹47,500.
        var r = _engine.Compute(RuleSetFixture.Salaried(1_000_000m, deductions: D("80G", 300_000m, "50_with_limit")), Regime.Old);

        r.Trace.Should().Contain(t => t.Step == "Deduction.80G" && t.Amount == 47_500m);
    }

    [Fact]
    public void Section80G_defaults_to_50pct_with_limit_when_subtype_is_blank()
    {
        // ₹40k donation is within the ₹95k qualifying limit, so the default 50% applies ⇒ ₹20,000.
        var r = _engine.Compute(RuleSetFixture.Salaried(1_000_000m, deductions: D("80G", 40_000m)), Regime.Old);

        r.Trace.Should().Contain(t => t.Step == "Deduction.80G" && t.Amount == 20_000m);
    }

    [Fact]
    public void Section80G_is_disallowed_under_the_new_regime()
    {
        var r = _engine.Compute(RuleSetFixture.Salaried(1_000_000m, deductions: D("80G", 50_000m, "100_no_limit")), Regime.New);

        r.Trace.Should().NotContain(t => t.Step == "Deduction.80G");
    }
}
