using FluentAssertions;
using TallyG.Tax.Domain.Enums;
using TallyG.Tax.Domain.TaxEngine;
using Xunit;

namespace TallyG.Tax.Tests.TaxEngine;

/// <summary>
/// Alternate Minimum Tax (s.115JC/JD) and reliefs u/s 89(1) and 90/90A/91, hand-computed against the
/// AY2025-26 fixture (OLD slabs: 0/5/20/30; cess 4%; AMT 18.5%; AMT threshold ₹20L; surcharge 10% > ₹50L).
/// </summary>
public class AmtAndReliefTests
{
    private readonly ITaxCalculator _engine = new TaxCalculator();

    private static DeductionInput[] ProfitLinked(decimal amount) => new[] { new DeductionInput("80-IAC", amount) };

    // ----------------------------------------------------------------- AMT (s.115JC)

    [Fact]
    public void Amt_is_payable_when_a_profit_linked_deduction_drops_regular_tax_below_AMT()
    {
        // Salary ₹50L − ₹50k std = ₹49.5L; less 80-IAC ₹35L ⇒ normal taxable ₹14.5L.
        // Adjusted total income adds the ₹35L back = ₹49.5L; AMT @18.5% = ₹9,15,750 + 4% cess = ₹9,52,380
        // (no surcharge, ATI < ₹50L). That exceeds the regular tax (₹2,57,400) ⇒ AMT is payable.
        var input = RuleSetFixture.Salaried(5_000_000m, deductions: ProfitLinked(3_500_000m));

        var r = _engine.Compute(input, Regime.Old);

        r.AdjustedTotalIncome.Should().Be(4_950_000m);
        r.AlternativeMinimumTax.Should().Be(952_380m);
        r.TotalTax.Should().Be(952_380m);            // AMT replaces the (lower) regular tax
        r.AmtCreditGenerated.Should().Be(694_980m);  // 9,52,380 − 2,57,400 regular ⇒ carried forward (115JD)
        r.Trace.Should().Contain(t => t.Step == "AMT.CreditGenerated");
    }

    [Fact]
    public void Amt_is_not_applicable_when_adjusted_total_income_is_within_the_20_lakh_threshold()
    {
        // Salary ₹15L − ₹50k = ₹14.5L; less 80-IAC ₹8L ⇒ taxable ₹6.5L; ATI = ₹14.5L ≤ ₹20L ⇒ no AMT.
        var r = _engine.Compute(RuleSetFixture.Salaried(1_500_000m, deductions: ProfitLinked(800_000m)), Regime.Old);

        r.AlternativeMinimumTax.Should().Be(0m);
        r.AmtCreditGenerated.Should().Be(0m);
        r.Trace.Should().Contain(t => t.Step == "AMT.NotApplicable");
    }

    [Fact]
    public void Amt_does_not_apply_under_the_new_regime()
    {
        var r = _engine.Compute(RuleSetFixture.Salaried(5_000_000m, deductions: ProfitLinked(3_500_000m)), Regime.New);

        r.AlternativeMinimumTax.Should().Be(0m);
        r.AmtCreditGenerated.Should().Be(0m);
    }

    [Fact]
    public void Amt_is_skipped_entirely_without_any_profit_linked_deduction()
    {
        // A plain ₹60L salary (no Part-C deduction) must NOT trip AMT even though 18.5%×ATI is large.
        var r = _engine.Compute(RuleSetFixture.Salaried(6_000_000m), Regime.Old);

        r.AlternativeMinimumTax.Should().Be(0m);
    }

    [Fact]
    public void Brought_forward_amt_credit_sets_off_when_regular_tax_exceeds_AMT()
    {
        // Salary ₹60L − ₹50k = ₹59.5L; less 80-IAC ₹5L ⇒ taxable ₹54.5L. ATI = ₹59.5L (AMT computed),
        // but the regular tax (~₹16.56L) exceeds AMT (~₹12.59L), so a brought-forward AMT credit can
        // be set off — limited to (regular − AMT). We assert the set-off via the with/without delta.
        var baseInput = RuleSetFixture.Salaried(6_000_000m, deductions: ProfitLinked(500_000m));
        var withCredit = baseInput with { BroughtForwardAmtCredit = 200_000m };

        var withoutR = _engine.Compute(baseInput, Regime.Old);
        var withR = _engine.Compute(withCredit, Regime.Old);

        withR.AlternativeMinimumTax.Should().BeGreaterThan(0m);   // AMT computed...
        withR.AmtCreditGenerated.Should().Be(0m);                 // ...but regular tax is higher
        withR.AmtCreditSetOff.Should().Be(200_000m);
        (withoutR.TotalTax - withR.TotalTax).Should().Be(200_000m);
    }

    // ----------------------------------------------------------------- Section 89(1) calculator

    [Fact]
    public void Section89_relief_is_the_excess_of_current_year_extra_tax_over_origin_year_extra_tax()
    {
        // Arrears add ₹1,00,000 tax this year but only ₹30,000 across the years they belong to.
        var relief = Section89Calculator.ComputeRelief(
            currentYearTaxWithArrears: 200_000m,
            currentYearTaxWithoutArrears: 100_000m,
            priorYears: new[] { new Section89Calculator.YearTax(120_000m, 90_000m) });

        relief.Should().Be(70_000m);
    }

    [Fact]
    public void Section89_relief_is_zero_when_spreading_does_not_reduce_tax()
    {
        var relief = Section89Calculator.ComputeRelief(
            currentYearTaxWithArrears: 150_000m,
            currentYearTaxWithoutArrears: 100_000m,                 // +50,000 this year
            priorYears: new[] { new Section89Calculator.YearTax(180_000m, 120_000m) }); // +60,000 in origin years

        relief.Should().Be(0m); // 50,000 − 60,000 = −10,000 ⇒ no relief
    }

    [Fact]
    public void Section89_relief_sums_across_multiple_origin_years()
    {
        var relief = Section89Calculator.ComputeRelief(
            currentYearTaxWithArrears: 300_000m,
            currentYearTaxWithoutArrears: 150_000m,                 // +150,000 this year
            priorYears: new[]
            {
                new Section89Calculator.YearTax(110_000m, 90_000m), // +20,000
                new Section89Calculator.YearTax(130_000m, 100_000m),// +30,000
            });

        relief.Should().Be(100_000m); // 150,000 − (20,000 + 30,000)
    }

    [Fact]
    public void Relief89_reduces_the_total_tax_when_supplied_to_the_engine()
    {
        var baseInput = RuleSetFixture.Salaried(2_000_000m);          // regular tax ₹4,13,400
        var withRelief = baseInput with { Relief89 = 50_000m };

        var r = _engine.Compute(withRelief, Regime.Old);

        r.Relief89.Should().Be(50_000m);
        r.TotalTax.Should().Be(363_400m);                            // 4,13,400 − 50,000
        r.Trace.Should().Contain(t => t.Step == "Relief.89");
    }

    // ----------------------------------------------------------------- Section 90/90A/91 (FTC)

    [Fact]
    public void Ftc_uses_the_lower_indian_rate_when_indian_rate_is_below_the_foreign_rate()
    {
        var trace = new List<TraceLine>();
        var r = ForeignTaxCreditCalculator.Compute(
            doublyTaxedForeignIncome: 500_000m,
            foreignTaxPaid: 150_000m,            // foreign rate 30%
            indianTaxBeforeRelief: 300_000m,
            totalTaxableIncome: 1_500_000m,      // average Indian rate 20%
            dtaaApplies: true,
            trace);

        r.Section.Should().Be("90/90A");
        r.Relief.Should().Be(100_000m);          // 5,00,000 × min(20%, 30%)
    }

    [Fact]
    public void Ftc_is_capped_at_the_foreign_tax_paid_when_foreign_rate_is_lower()
    {
        var trace = new List<TraceLine>();
        var r = ForeignTaxCreditCalculator.Compute(
            doublyTaxedForeignIncome: 500_000m,
            foreignTaxPaid: 40_000m,             // foreign rate 8%
            indianTaxBeforeRelief: 300_000m,
            totalTaxableIncome: 1_500_000m,      // average Indian rate 20%
            dtaaApplies: false,
            trace);

        r.Section.Should().Be("91");
        r.Relief.Should().Be(40_000m);           // min(5,00,000 × 8%, foreign tax 40,000)
    }

    [Fact]
    public void Foreign_tax_credit_reduces_the_total_tax_via_the_engine()
    {
        // ₹20L salary, old regime, regular tax ₹4,13,400 (avg rate ≈21.2%). Foreign income ₹3L taxed
        // abroad at 10% (₹30k) — the lower (foreign) rate binds, so relief = ₹30,000.
        var input = RuleSetFixture.Salaried(2_000_000m) with
        {
            ForeignIncomeDoublyTaxed = 300_000m,
            ForeignTaxPaid = 30_000m,
            ForeignDtaaApplies = true,
        };

        var r = _engine.Compute(input, Regime.Old);

        r.Relief90And91.Should().Be(30_000m);
        r.TotalTax.Should().Be(383_400m);        // 4,13,400 − 30,000
        r.Trace.Should().Contain(t => t.Step == "Relief.90");
    }
}
