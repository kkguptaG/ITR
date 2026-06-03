using FluentAssertions;
using TallyG.Tax.Api.Modules.Returns;
using TallyG.Tax.Domain.Enums;
using Xunit;

namespace TallyG.Tax.Tests.Returns;

/// <summary>
/// The ITR auto-selector cascade (<see cref="ItrSelectorService.EvaluateCascade"/>) — a pure function of
/// the feature flags. Anchors the baseline ITR-1 case and the special-rate disqualifiers that must push a
/// return off Sahaj/Sugam onto ITR-2: s.115BB/115BBJ winnings and crypto/VDA gains.
/// </summary>
public class ItrSelectorCascadeTests
{
    private static ItrSelectorInput Salaried(decimal income = 800_000m) => new()
    {
        TotalIncome = income,
        HasSalaryOrPension = true,
        HousePropertyCount = 1,
    };

    [Fact]
    public void Plain_salaried_profile_maps_to_ITR1()
    {
        var v = ItrSelectorService.EvaluateCascade(Salaried());

        v.RecommendedForm.Should().Be(ItrType.ITR1);
        v.BlockedForms.Should().NotContainKey("ITR-1");
    }

    [Fact]
    public void Salaried_with_winnings_is_forced_off_ITR1_to_ITR2()
    {
        // A lottery (s.115BB) or online-game (s.115BBJ) win is special-rate income Sahaj/Sugam can't report.
        var v = ItrSelectorService.EvaluateCascade(Salaried() with { HasWinnings = true });

        v.RecommendedForm.Should().Be(ItrType.ITR2);
        v.DecidingFlags.Should().Contain("has_winnings");
        v.BlockedForms["ITR-1"].Should().Contain("has_winnings");
        v.BlockedForms["ITR-4"].Should().Contain("has_winnings");
    }

    [Fact]
    public void Crypto_vda_gains_are_forced_off_ITR1_to_ITR2()
    {
        var v = ItrSelectorService.EvaluateCascade(Salaried() with { HasCryptoVda = true });

        v.RecommendedForm.Should().Be(ItrType.ITR2);
        v.DecidingFlags.Should().Contain("has_crypto_vda");
        v.BlockedForms["ITR-1"].Should().Contain("has_crypto_vda");
        v.BlockedForms["ITR-4"].Should().Contain("has_crypto_vda");
    }

    [Fact]
    public void A_small_LTCG_112A_alone_still_rides_on_ITR1()
    {
        // Baseline for the relaxation: only a small LTCG-112A within the ₹1.25L threshold → Sahaj is allowed.
        var input = Salaried() with
        {
            HasCapitalGains = true,
            CapitalGainsOnlyLtcg112A = true,
            Ltcg112AAmount = 50_000m,
        };

        ItrSelectorService.EvaluateCascade(input).RecommendedForm.Should().Be(ItrType.ITR1);
    }

    [Fact]
    public void Winnings_revoke_the_small_LTCG_112A_ride_along()
    {
        // The same small LTCG-112A, but coexisting winnings revoke the ITR-1 relaxation → ITR-2.
        var input = Salaried() with
        {
            HasCapitalGains = true,
            CapitalGainsOnlyLtcg112A = true,
            Ltcg112AAmount = 50_000m,
            HasWinnings = true,
        };

        ItrSelectorService.EvaluateCascade(input).RecommendedForm.Should().Be(ItrType.ITR2);
    }
}
