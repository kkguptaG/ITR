using FluentAssertions;
using TallyG.Tax.Domain.Enums;
using TallyG.Tax.Domain.TaxEngine;
using Xunit;

namespace TallyG.Tax.Tests.TaxEngine;

/// <summary>
/// The 80C/80D deduction advisor: it must surface BOTH the self and the (separately-capped) parents'
/// 80D cover, and the self headroom must respect the age-aware cap (senior ₹50k vs ₹25k).
/// </summary>
public class DeductionRecommenderTests
{
    private readonly ITaxCalculator _engine = new TaxCalculator();

    [Fact]
    public void Recommends_both_self_and_parents_80D_cover()
    {
        var r = DeductionRecommender.Recommend(_engine, RuleSetFixture.Salaried(1_500_000m));

        r.Suggestions.Should().Contain(s => s.Section == "80D");         // self
        r.Suggestions.Should().Contain(s => s.Section == "80D_PARENTS"); // parents — a separate 80D limit
    }

    [Fact]
    public void Senior_80D_self_headroom_uses_the_50k_cap()
    {
        var senior = DeductionRecommender.Recommend(_engine, RuleSetFixture.Salaried(1_500_000m, age: 65));
        var young = DeductionRecommender.Recommend(_engine, RuleSetFixture.Salaried(1_500_000m, age: 35));

        senior.Suggestions.Single(s => s.Section == "80D").GapToInvest.Should().Be(50_000m);
        young.Suggestions.Single(s => s.Section == "80D").GapToInvest.Should().Be(25_000m);
    }

    [Fact]
    public void Recommends_80EEA_when_a_house_property_exists_and_80EEA_is_unclaimed()
    {
        // A salaried profile with a self-occupied property → 80EEA should be surfaced (₹1.5L headroom).
        var inputWithHouse = RuleSetFixture.Salaried(1_500_000m) with
        {
            HouseProperties = new[] { new HousePropertyInput(HousePropertyType.SelfOccupied, 0m, 0m, 100_000m) },
        };
        var r = DeductionRecommender.Recommend(_engine, inputWithHouse);
        r.Suggestions.Should().Contain(s => s.Section == "80EEA" && s.GapToInvest == 150_000m);
    }

    [Fact]
    public void Recommends_80TTB_not_80TTA_for_a_senior_citizen()
    {
        var seniorInput = RuleSetFixture.Salaried(1_500_000m, age: 65);
        var r = DeductionRecommender.Recommend(_engine, seniorInput);
        r.Suggestions.Should().Contain(s => s.Section == "80TTB", "seniors get the ₹50k 80TTB cap");
        r.Suggestions.Should().NotContain(s => s.Section == "80TTA", "80TTA is not available to seniors");
    }

    [Fact]
    public void Recommends_80TTA_not_80TTB_for_a_non_senior()
    {
        var youngInput = RuleSetFixture.Salaried(1_500_000m, age: 35);
        var r = DeductionRecommender.Recommend(_engine, youngInput);
        r.Suggestions.Should().Contain(s => s.Section == "80TTA");
        r.Suggestions.Should().NotContain(s => s.Section == "80TTB", "80TTB is for seniors only");
    }

    [Fact]
    public void Does_not_recommend_80GG_at_high_income_because_10pct_floor_eliminates_the_deduction()
    {
        // 80GG = min(₹5k/month, 25% income, actual_rent − 10% income). At ₹10L gross the taxable income
        // is ~₹9.25L → 10% = ₹92.5k > ₹60k cap → the effective deduction is zero → no saving → not surfaced.
        var r = DeductionRecommender.Recommend(_engine, RuleSetFixture.Salaried(1_000_000m));
        r.Suggestions.Should().NotContain(s => s.Section == "80GG",
            "80GG's rent-minus-10%-income formula yields zero saving at ₹10L income, so the advisor correctly omits it");
    }
}
