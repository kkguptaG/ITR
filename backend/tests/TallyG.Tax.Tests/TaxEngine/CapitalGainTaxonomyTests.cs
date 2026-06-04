using FluentAssertions;
using TallyG.Tax.Domain.Enums;
using TallyG.Tax.Domain.TaxEngine;
using Xunit;

namespace TallyG.Tax.Tests.TaxEngine;

/// <summary>
/// The two-level taxonomy (Ch.3 §3.6): every fine-grained <see cref="CapitalGainSubType"/> maps to a
/// broad tax-behaviour <see cref="CapitalGainAssetType"/> the engine routes on, so "a new asset is data,
/// not a code change". Also verifies the foreign + STT defaults a sub-type implies.
/// </summary>
public class CapitalGainTaxonomyTests
{
    [Theory]
    // Equity family → listed-equity (111A/112A) path …
    [InlineData(CapitalGainSubType.ListedShare, CapitalGainAssetType.ListedEquity)]
    [InlineData(CapitalGainSubType.EsopShare, CapitalGainAssetType.ListedEquity)]
    [InlineData(CapitalGainSubType.RsuShare, CapitalGainAssetType.ListedEquity)]
    [InlineData(CapitalGainSubType.IpoShare, CapitalGainAssetType.ListedEquity)]
    [InlineData(CapitalGainSubType.BonusShare, CapitalGainAssetType.ListedEquity)]
    // … except genuinely unlisted / buyback → unlisted-shares (s.112) path.
    [InlineData(CapitalGainSubType.UnlistedShare, CapitalGainAssetType.UnlistedShares)]
    [InlineData(CapitalGainSubType.Buyback, CapitalGainAssetType.UnlistedShares)]
    // Mutual funds: equity-oriented (incl. hybrid) vs debt/international.
    [InlineData(CapitalGainSubType.EquityMutualFund, CapitalGainAssetType.EquityMutualFund)]
    [InlineData(CapitalGainSubType.HybridMutualFund, CapitalGainAssetType.EquityMutualFund)]
    [InlineData(CapitalGainSubType.DebtMutualFund, CapitalGainAssetType.DebtMutualFund)]
    [InlineData(CapitalGainSubType.InternationalFund, CapitalGainAssetType.DebtMutualFund)]
    // Real estate.
    [InlineData(CapitalGainSubType.ResidentialHouse, CapitalGainAssetType.ImmovableProperty)]
    [InlineData(CapitalGainSubType.CommercialProperty, CapitalGainAssetType.ImmovableProperty)]
    [InlineData(CapitalGainSubType.Plot, CapitalGainAssetType.ImmovableProperty)]
    [InlineData(CapitalGainSubType.AgriculturalLand, CapitalGainAssetType.AgriculturalLand)]
    // Gold & precious.
    [InlineData(CapitalGainSubType.PhysicalGold, CapitalGainAssetType.Gold)]
    [InlineData(CapitalGainSubType.GoldEtf, CapitalGainAssetType.Gold)]
    [InlineData(CapitalGainSubType.SovereignGoldBond, CapitalGainAssetType.Bonds)]
    [InlineData(CapitalGainSubType.Jewellery, CapitalGainAssetType.Jewellery)]
    // Bonds.
    [InlineData(CapitalGainSubType.GovernmentSecurity, CapitalGainAssetType.Bonds)]
    [InlineData(CapitalGainSubType.Debenture, CapitalGainAssetType.Bonds)]
    // VDA.
    [InlineData(CapitalGainSubType.Crypto, CapitalGainAssetType.CryptoVda)]
    [InlineData(CapitalGainSubType.Nft, CapitalGainAssetType.CryptoVda)]
    // Foreign equity → unlisted-shares behaviour (no STT).
    [InlineData(CapitalGainSubType.ForeignShare, CapitalGainAssetType.UnlistedShares)]
    [InlineData(CapitalGainSubType.UsStock, CapitalGainAssetType.UnlistedShares)]
    [InlineData(CapitalGainSubType.AdrGdr, CapitalGainAssetType.UnlistedShares)]
    // Other.
    [InlineData(CapitalGainSubType.Goodwill, CapitalGainAssetType.Other)]
    [InlineData(CapitalGainSubType.SlumpSale, CapitalGainAssetType.Other)]
    public void CategoryOf_maps_subtype_to_tax_behaviour_category(CapitalGainSubType subType, CapitalGainAssetType expected)
        => CapitalGainTaxonomy.CategoryOf(subType).Should().Be(expected);

    [Fact]
    public void Every_subtype_maps_to_a_category_without_throwing()
    {
        foreach (CapitalGainSubType st in Enum.GetValues(typeof(CapitalGainSubType)))
        {
            var act = () => CapitalGainTaxonomy.CategoryOf(st);
            act.Should().NotThrow($"sub-type {st} must map to a tax category");
        }
    }

    [Theory]
    [InlineData(CapitalGainSubType.ForeignShare, true)]
    [InlineData(CapitalGainSubType.UsStock, true)]
    [InlineData(CapitalGainSubType.AdrGdr, true)]
    [InlineData(CapitalGainSubType.ForeignRsu, true)]
    [InlineData(CapitalGainSubType.ListedShare, false)]
    [InlineData(CapitalGainSubType.ResidentialHouse, false)]
    public void IsForeign_flags_assets_held_outside_India(CapitalGainSubType subType, bool expected)
        => CapitalGainTaxonomy.IsForeign(subType).Should().Be(expected);

    [Theory]
    [InlineData(CapitalGainSubType.ListedShare, true)]
    [InlineData(CapitalGainSubType.EquityMutualFund, true)]
    [InlineData(CapitalGainSubType.UnlistedShare, false)]
    [InlineData(CapitalGainSubType.DebtMutualFund, false)]
    [InlineData(CapitalGainSubType.ForeignShare, false)]
    public void SttTypicallyPaid_only_for_listed_equity_and_equity_mf(CapitalGainSubType subType, bool expected)
        => CapitalGainTaxonomy.SttTypicallyPaid(subType).Should().Be(expected);
}
