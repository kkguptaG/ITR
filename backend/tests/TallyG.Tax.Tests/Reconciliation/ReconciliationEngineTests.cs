using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using TallyG.Tax.Api.Modules.Reconciliation;
using Xunit;

namespace TallyG.Tax.Tests.Reconciliation;

/// <summary>
/// The pure pre-filing reconciliation engine: return-side heads vs the department's AIS / 26AS field maps,
/// classified matched / under-reported / over-reported. Covers the heads added beyond the original set
/// (other + refund interest, rent, TCS, self-assessment tax).
/// </summary>
public class ReconciliationEngineTests
{
    private static readonly IReadOnlyDictionary<string, decimal> Empty = new Dictionary<string, decimal>();

    private static ReconciliationInputs Inputs(
        decimal grossSalary = 0, decimal savings = 0, decimal fd = 0, decimal otherInterest = 0,
        decimal refundInterest = 0, decimal dividend = 0, decimal rent = 0, decimal securities = 0,
        decimal tds = 0, decimal advance = 0, decimal sat = 0, decimal tcs = 0)
        => new(grossSalary, savings, fd, otherInterest, refundInterest, dividend, rent, securities, tds, advance, sat, tcs);

    private static ReconLineDto Line(ReconciliationReportDto r, string label) => r.Lines.Single(l => l.Label == label);

    [Fact]
    public void Figures_matching_AIS_and_26AS_report_no_mismatch()
    {
        var ais = new Dictionary<string, decimal> { ["ais.salary_gross"] = 1_000_000m, ["ais.dividend_income"] = 8_000m };
        var as26 = new Dictionary<string, decimal> { ["form26as.tds_salary"] = 50_000m };

        var r = ReconciliationEngine.BuildReport(Inputs(grossSalary: 1_000_000m, dividend: 8_000m, tds: 50_000m), ais, as26);

        r.HasSources.Should().BeTrue();
        r.MismatchCount.Should().Be(0);
        r.UnderReportedCount.Should().Be(0);
    }

    [Fact]
    public void Income_lower_than_AIS_is_flagged_under_reported()
    {
        var ais = new Dictionary<string, decimal> { ["ais.dividend_income"] = 40_000m };
        var r = ReconciliationEngine.BuildReport(Inputs(dividend: 8_000m), ais, Empty);

        Line(r, "Dividend").Status.Should().Be("under_reported");
        r.UnderReportedCount.Should().Be(1);
    }

    [Fact]
    public void TCS_in_26AS_not_fully_claimed_is_flagged_with_a_TCS_specific_note()
    {
        var as26 = new Dictionary<string, decimal> { ["form26as.tcs"] = 25_000m };
        var r = ReconciliationEngine.BuildReport(Inputs(tcs: 0m), Empty, as26);

        var line = Line(r, "TCS credit");
        line.Status.Should().Be("under_reported");
        line.Source.Should().Be("26AS");
        line.Note.Should().Contain("TCS");
    }

    [Fact]
    public void Rent_other_interest_refund_interest_and_self_assessment_reconcile_as_new_heads()
    {
        var ais = new Dictionary<string, decimal>
        {
            ["ais.rent_received"] = 300_000m,
            ["ais.interest_others"] = 12_000m,
            ["ais.interest_income_tax_refund"] = 1_500m,
        };
        var as26 = new Dictionary<string, decimal> { ["form26as.self_assessment_tax"] = 20_000m };

        var r = ReconciliationEngine.BuildReport(
            Inputs(rent: 300_000m, otherInterest: 12_000m, refundInterest: 1_500m, sat: 20_000m), ais, as26);

        Line(r, "Rent received").Status.Should().Be("matched");
        Line(r, "Interest — other").Status.Should().Be("matched");
        Line(r, "Interest — income-tax refund").Status.Should().Be("matched");
        Line(r, "Self-assessment tax").Status.Should().Be("matched");
        r.MismatchCount.Should().Be(0);
    }

    [Fact]
    public void A_head_the_department_does_not_report_produces_no_line()
    {
        // No AIS rent key on file → no rent line even though the return declares rent.
        var r = ReconciliationEngine.BuildReport(Inputs(rent: 300_000m), Empty, Empty);
        r.Lines.Should().NotContain(l => l.Label == "Rent received");
    }

    [Fact]
    public void Securities_sale_value_below_the_AIS_SFT_is_flagged_under_reported()
    {
        // AIS SFT shows ₹8L of securities sales but the return only declares ₹3L of sale consideration —
        // a redemption the department knows about is missing.
        var ais = new Dictionary<string, decimal> { ["ais.sft_sale_of_securities"] = 800_000m };
        var r = ReconciliationEngine.BuildReport(Inputs(securities: 300_000m), ais, Empty);

        var line = Line(r, "Securities / MF sales (sale value)");
        line.Status.Should().Be("under_reported");
        line.Source.Should().Be("AIS");
        r.UnderReportedCount.Should().Be(1);
    }
}
