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
        // Payable must reflect the interest: refund/payable = prepaid − tax − interest = −(tax + interest).
        r.RefundOrPayable.Should().Be(-(r.TotalTax + r.InterestPenalty));
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

        interest.Should().BeGreaterThan(0m);
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
}
