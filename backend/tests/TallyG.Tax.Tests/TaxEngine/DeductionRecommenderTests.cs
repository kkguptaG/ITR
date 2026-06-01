using FluentAssertions;
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
}
