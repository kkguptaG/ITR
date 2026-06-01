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
}
