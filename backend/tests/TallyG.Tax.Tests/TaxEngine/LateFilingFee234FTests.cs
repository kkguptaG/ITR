using FluentAssertions;
using TallyG.Tax.Domain.Enums;
using TallyG.Tax.Domain.TaxEngine;
using Xunit;

namespace TallyG.Tax.Tests.TaxEngine;

/// <summary>
/// s.234F late-filing fee: a flat fee charged when the return is furnished after the s.139(1) due date —
/// ₹5,000 when total income exceeds ₹5L, ₹1,000 below it, nil for an on-time return or income within the
/// basic exemption. Unlike s.234A/B/C interest it applies even when a refund is due. Folded into the
/// refund/payable. Computed against the AY2025-26 fixture (due date 31-Jul-2025).
/// </summary>
public class LateFilingFee234FTests
{
    private readonly ITaxCalculator _engine = new TaxCalculator();
    private static readonly DateOnly DueDate = new(2025, 7, 31);
    private static readonly DateOnly LateDate = new(2025, 12, 15);
    private static readonly DateOnly OnTimeDate = new(2025, 7, 20);

    [Fact]
    public void Full_fee_5000_when_filed_late_and_income_above_5L()
    {
        var input = RuleSetFixture.Salaried(1_000_000m) with
        {
            FilingDueDate = DueDate,
            ActualFilingDate = LateDate,
        };

        _engine.Compute(input, Regime.New).LateFilingFee234F.Should().Be(5_000m);
    }

    [Fact]
    public void Reduced_fee_1000_when_filed_late_and_income_at_or_below_5L()
    {
        // gross 5,50,000 − 75,000 std = 4,75,000 taxable (> 3L exemption, ≤ 5L) ⇒ reduced ₹1,000.
        var input = RuleSetFixture.Salaried(550_000m) with
        {
            FilingDueDate = DueDate,
            ActualFilingDate = LateDate,
        };

        _engine.Compute(input, Regime.New).LateFilingFee234F.Should().Be(1_000m);
    }

    [Fact]
    public void No_fee_for_an_on_time_return()
    {
        var input = RuleSetFixture.Salaried(1_000_000m) with
        {
            FilingDueDate = DueDate,
            ActualFilingDate = OnTimeDate,
        };

        _engine.Compute(input, Regime.New).LateFilingFee234F.Should().Be(0m);
    }

    [Fact]
    public void No_fee_when_dates_are_absent_a_draft_with_no_filing_date()
    {
        _engine.Compute(RuleSetFixture.Salaried(1_000_000m), Regime.New).LateFilingFee234F.Should().Be(0m);
    }

    [Fact]
    public void No_fee_when_total_income_is_within_the_basic_exemption()
    {
        // gross 2,50,000 − 75,000 std = 1,75,000 taxable, within the ₹3L new-regime exemption ⇒ no obligation, no fee.
        var input = RuleSetFixture.Salaried(250_000m) with
        {
            FilingDueDate = DueDate,
            ActualFilingDate = LateDate,
        };

        _engine.Compute(input, Regime.New).LateFilingFee234F.Should().Be(0m);
    }

    [Fact]
    public void Fee_reduces_the_refund_even_though_no_interest_is_due()
    {
        // High TDS ⇒ a refund (no 234A/B/C). Filing late still attracts the flat ₹5,000 fee, so the only
        // difference between the on-time and late computations is the fee.
        var onTime = RuleSetFixture.Salaried(1_000_000m, tdsPaid: 100_000m) with
        {
            FilingDueDate = DueDate,
            ActualFilingDate = OnTimeDate,
        };
        var late = onTime with { ActualFilingDate = LateDate };

        var rOnTime = _engine.Compute(onTime, Regime.New);
        var rLate = _engine.Compute(late, Regime.New);

        rOnTime.RefundOrPayable.Should().BeGreaterThan(0m, "high TDS produces a refund");
        rLate.LateFilingFee234F.Should().Be(5_000m);
        (rOnTime.RefundOrPayable - rLate.RefundOrPayable).Should().Be(5_000m,
            "the late fee is the only difference and it reduces the refund rupee-for-rupee");
    }
}
