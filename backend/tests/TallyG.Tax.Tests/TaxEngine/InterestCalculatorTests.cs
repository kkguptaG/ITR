using System.Linq;
using FluentAssertions;
using TallyG.Tax.Domain.Enums;
using TallyG.Tax.Domain.TaxEngine;
using Xunit;

namespace TallyG.Tax.Tests.TaxEngine;

/// <summary>
/// Tests for s.234A/B/C interest, exercised through the full engine. Inputs are built via the
/// record initializer (the same shape TaxService produces), with the AY running
/// PY 1-Apr-2025 → 31-Mar-2026, s.139(1) due date 31-Jul-2026.
/// </summary>
public class InterestCalculatorTests
{
    private readonly ITaxCalculator _engine = new TaxCalculator();

    private static readonly DateOnly PyStart = new(2025, 4, 1);
    private static readonly DateOnly PyEnd = new(2026, 3, 31);
    private static readonly DateOnly DueDate = new(2026, 7, 31);

    private static TaxComputationInput DatedSalaried(
        decimal gross, DateOnly filed, decimal advance = 0m, AdvanceTaxInstallmentInput[]? installments = null)
        => new()
        {
            AssessmentYearCode = "AY2025-26",
            RuleSetVersion = "1.0.0",
            RulesJson = RuleSetFixture.Ay2025_26Json,
            Age = 35,
            Salaries = new[] { new SalaryInput("Acme Corp", gross, 0m, 0m, 0m, 0m) },
            AdvanceTaxPaid = advance,
            FilingDueDate = DueDate,
            ActualFilingDate = filed,
            PreviousYearStart = PyStart,
            PreviousYearEnd = PyEnd,
            AdvanceTaxInstallments = installments ?? System.Array.Empty<AdvanceTaxInstallmentInput>(),
        };

    [Fact]
    public void No_advance_tax_and_late_filing_triggers_234A_234B_and_234C()
    {
        // ₹12L salary, old regime → real tax due; no TDS/advance; filed ~4 months late.
        var r = _engine.Compute(DatedSalaried(1_200_000m, new DateOnly(2026, 11, 20)), Regime.Old);

        r.TotalTax.Should().BeGreaterThan(150_000m, "12L salary in the old regime owes tax");
        r.InterestPenalty.Should().BeGreaterThan(0m,
            "234A/B/C apply; trace = {0}", string.Join(",", r.Trace.Select(t => t.Step)));
        r.Trace.Should().Contain(t => t.Step == "Interest.234A");
        r.Trace.Should().Contain(t => t.Step == "Interest.234B");
        r.Trace.Should().Contain(t => t.Step == "Interest.234C");
        // A belated return with income above ₹5L also attracts the flat s.234F late-filing fee.
        r.LateFilingFee234F.Should().Be(5_000m);
        // Payable must reflect interest AND the fee: payable = prepaid − tax − interest − fee.
        r.RefundOrPayable.Should().Be(-(r.TotalTax + r.InterestPenalty + r.LateFilingFee234F));
    }

    [Fact]
    public void Filed_on_time_with_full_advance_tax_paid_early_has_no_interest()
    {
        var tax = _engine.Compute(DatedSalaried(1_200_000m, DueDate), Regime.Old).TotalTax;

        // 100% of the tax paid as advance on the very first installment date, filed before the due date.
        var input = DatedSalaried(
            1_200_000m,
            new DateOnly(2026, 7, 30),
            advance: tax,
            installments: new[] { new AdvanceTaxInstallmentInput(new DateOnly(2025, 6, 15), tax) });

        _engine.Compute(input, Regime.Old).InterestPenalty.Should().Be(0m);
    }

    [Fact]
    public void No_interest_dates_means_no_interest()
    {
        // Without the filing/PY dates the engine cannot (and must not) compute interest.
        _engine.Compute(RuleSetFixture.Salaried(1_200_000m), Regime.Old).InterestPenalty.Should().Be(0m);
    }

    [Fact]
    public void Direct_234A_late_filing_is_computed()
    {
        var rs = RuleSet.Parse(RuleSetFixture.Ay2025_26Json);
        var input = DatedSalaried(1_200_000m, new DateOnly(2026, 11, 20));
        var trace = new System.Collections.Generic.List<TraceLine>();

        var interest = InterestCalculator.Compute(input, 163_800m, rs, trace);

        interest.Total.Should().BeGreaterThan(0m);
        interest.S234A.Should().BeGreaterThan(0m);
        (interest.S234A + interest.S234B + interest.S234C).Should().Be(interest.Total); // split reconciles
        trace.Should().Contain(t => t.Step == "Interest.234A");
    }

    [Fact]
    public void Self_assessment_tax_does_not_reduce_the_234A_base()
    {
        // ₹12L old regime → tax ₹1,63,800; filed ~4 months late; a large self-assessment payment must
        // NOT shrink 234A (that late payment is exactly what 234A penalises). 234A = 1% × 4 × 1,63,800.
        var input = DatedSalaried(1_200_000m, new DateOnly(2026, 11, 20)) with { SelfAssessmentTaxPaid = 500_000m };
        var r = _engine.Compute(input, Regime.Old);

        r.Trace.Where(t => t.Step == "Interest.234A").Sum(t => t.Amount).Should().Be(6_552m);
    }

    private static TaxComputationInput DatedBusiness(
        decimal netProfit, DateOnly filed, decimal advance, AdvanceTaxInstallmentInput[] installments)
        => new()
        {
            AssessmentYearCode = "AY2025-26",
            RuleSetVersion = "1.0.0",
            RulesJson = RuleSetFixture.Ay2025_26Json,
            Age = 35,
            BusinessIncomes = new[] { new BusinessIncomeInput(false, null, 0m, 0m, 0m, netProfit, false) },
            AdvanceTaxPaid = advance,
            FilingDueDate = DueDate,
            ActualFilingDate = filed,
            PreviousYearStart = PyStart,
            PreviousYearEnd = PyEnd,
            AdvanceTaxInstallments = installments,
        };

    [Fact]
    public void S234C_charges_only_deferment_when_the_full_advance_is_paid_late_in_the_year()
    {
        // ₹15L business (new regime) → tax ₹1,45,600, no TDS. The full advance is paid — but ALL on the last
        // installment date (15-Mar) — so s.234B is nil (≥90% paid) yet s.234C deferment interest still runs
        // for Q1–Q3 (nothing was paid by 15-Jun / 15-Sep / 15-Dec):
        //   1% × (3×15% + 3×45% + 3×75%) × ₹1,45,600 = ₹5,896.8 → ₹5,897.
        var input = DatedBusiness(
            1_500_000m,
            filed: DueDate,
            advance: 145_600m,
            installments: new[] { new AdvanceTaxInstallmentInput(new DateOnly(2026, 3, 15), 145_600m) });
        var r = _engine.Compute(input, Regime.New);

        r.TotalTax.Should().Be(145_600m);
        r.Interest234C.Should().Be(5_897m);
        r.Interest234B.Should().Be(0m, "the full advance was paid (≥90% of assessed tax), so no 234B shortfall");
        r.Interest234A.Should().Be(0m, "filed on the due date");
    }

    [Fact]
    public void Nil_tax_return_filed_late_bears_no_interest()
    {
        // ₹5L salary (new regime): net ₹4.25L → slab ₹6,250 fully wiped by the s.87A rebate → nil tax. Even
        // filed months late, there is no 234A (nothing unpaid) and no 234B/234C (assessed tax is below the
        // s.208 advance-tax threshold).
        var r = _engine.Compute(DatedSalaried(500_000m, new DateOnly(2026, 12, 31)), Regime.New);

        r.TotalTax.Should().Be(0m);
        r.InterestPenalty.Should().Be(0m);
        r.Interest234A.Should().Be(0m);
        r.Interest234B.Should().Be(0m);
        r.Interest234C.Should().Be(0m);
    }
}
