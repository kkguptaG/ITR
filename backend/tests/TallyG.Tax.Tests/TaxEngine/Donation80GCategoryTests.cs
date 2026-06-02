using FluentAssertions;
using TallyG.Tax.Domain.Enums;
using TallyG.Tax.Domain.TaxEngine;
using Xunit;

namespace TallyG.Tax.Tests.TaxEngine;

/// <summary>
/// Locks the contract between TaxComputationInputFactory (which maps donee-wise 80G categories to these
/// exact SubType strings) and the engine's 80G categoriser. A 100%-no-limit donation deducts in full;
/// a 50% donation halves; the "with limit" categories are capped, in aggregate, at 10% of adjusted GTI.
/// 80G is OLD-regime only.
/// </summary>
public class Donation80GCategoryTests
{
    private readonly ITaxCalculator _engine = new TaxCalculator();

    [Fact]
    public void Donee_categories_drive_the_80G_deduction_so_a_100pct_no_limit_gift_is_not_under_claimed()
    {
        // Mirrors the demo's donee split: PM CARES ₹5,000 (100%, no limit) + a charitable trust ₹6,000
        // (50%, with limit). At this income the 10%-of-GTI limit doesn't bind, so the allowed 80G deduction
        // is 5,000 + 0.5×6,000 = ₹8,000 — NOT the ₹5,500 the category-less conservative default would give.
        var deductions = new[]
        {
            new DeductionInput("80G", 5_000m, "100_no_limit"),
            new DeductionInput("80G", 6_000m, "50_limit"),
        };

        var r = _engine.Compute(RuleSetFixture.Salaried(2_000_000m, deductions: deductions), Regime.Old);

        r.Trace.Should().Contain(t => t.Step == "Deduction.80G" && t.Amount == 8_000m);
    }

    [Fact]
    public void With_limit_donation_is_capped_at_10pct_of_adjusted_gti()
    {
        // A large 100%-with-limit donation against a modest income: the deduction is capped at 10% of the
        // adjusted GTI, so it is allowed only in part (strictly less than the amount donated).
        var deductions = new[] { new DeductionInput("80G", 5_000_000m, "100_limit") };

        var r = _engine.Compute(RuleSetFixture.Salaried(1_000_000m, deductions: deductions), Regime.Old);

        var line = r.Trace.Should().ContainSingle(t => t.Step == "Deduction.80G").Which;
        line.Amount.Should().BeGreaterThan(0m).And.BeLessThan(5_000_000m, "the with-limit donation is capped at 10% of adjusted GTI");
    }
}
