using FluentAssertions;
using TallyG.Tax.Domain.TaxEngine;
using Xunit;

namespace TallyG.Tax.Tests.TaxEngine;

/// <summary>
/// Share buy-back s.115QA treatment across the 1-Oct-2024 cutoff (Ch.3 §3.6).
/// </summary>
public class BuybackTreatmentTests
{
    private static readonly DateOnly Cutoff = new(2024, 10, 1);

    [Fact]
    public void Before_the_cutoff_the_buyback_is_exempt_in_the_shareholders_hands()
    {
        var r = BuybackTreatment.Resolve(buybackConsideration: 500000m, cost: 300000m, transferDate: new DateOnly(2024, 6, 1), cutoff: Cutoff);

        r.Exempt.Should().BeTrue();
        r.DeemedDividend.Should().Be(0m);
        r.CapitalLoss.Should().Be(0m);
    }

    [Fact]
    public void On_or_after_the_cutoff_it_is_a_deemed_dividend_plus_a_capital_loss_of_the_cost()
    {
        var r = BuybackTreatment.Resolve(buybackConsideration: 500000m, cost: 300000m, transferDate: new DateOnly(2024, 11, 1), cutoff: Cutoff);

        r.Exempt.Should().BeFalse();
        r.DeemedDividend.Should().Be(500000m); // taxed as Income from Other Sources (slab)
        r.CapitalLoss.Should().Be(300000m);    // cost becomes a capital loss
    }

    [Fact]
    public void Exactly_on_the_cutoff_date_is_treated_as_post_cutoff()
    {
        var r = BuybackTreatment.Resolve(500000m, 300000m, new DateOnly(2024, 10, 1), Cutoff);

        r.Exempt.Should().BeFalse();
        r.DeemedDividend.Should().Be(500000m);
    }

    [Fact]
    public void With_no_transfer_date_the_current_deemed_dividend_law_is_assumed()
    {
        var r = BuybackTreatment.Resolve(500000m, 300000m, transferDate: null, cutoff: Cutoff);

        r.Exempt.Should().BeFalse();
        r.DeemedDividend.Should().Be(500000m);
    }
}
