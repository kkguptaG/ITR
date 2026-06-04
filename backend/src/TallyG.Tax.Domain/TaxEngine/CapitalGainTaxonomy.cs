using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Domain.TaxEngine;

/// <summary>
/// Maps the fine-grained <see cref="CapitalGainSubType"/> to the broad tax-behaviour
/// <see cref="CapitalGainAssetType"/> the engine routes on (Ch.3 §3.6), plus the defaults a sub-type
/// implies (STT relevance, foreign-asset flag). This is the single seam through which "a new asset is
/// data, not a code change": the UI offers sub-types; the engine keeps switching on the category.
///
/// Special cases that need their OWN treatment rather than a category alias (buyback 115QA, SGB
/// maturity exemption, slump sale 50B) currently map to the nearest category and are refined in P7.
/// </summary>
public static class CapitalGainTaxonomy
{
    /// <summary>The tax-behaviour category the engine should compute this sub-type under.</summary>
    public static CapitalGainAssetType CategoryOf(CapitalGainSubType subType) => subType switch
    {
        // Equity — STT-paid listed instruments route through the 111A/112A equity path.
        CapitalGainSubType.ListedShare
            or CapitalGainSubType.IpoShare
            or CapitalGainSubType.EsopShare
            or CapitalGainSubType.RsuShare
            or CapitalGainSubType.BonusShare
            or CapitalGainSubType.RightsShare => CapitalGainAssetType.ListedEquity,
        CapitalGainSubType.UnlistedShare
            or CapitalGainSubType.Buyback => CapitalGainAssetType.UnlistedShares,

        // Mutual funds — equity-oriented (incl. hybrid, assumed equity-oriented) → equity path;
        // debt + international (non-equity) → debt MF (always slab post-1-Apr-2023).
        CapitalGainSubType.EquityMutualFund
            or CapitalGainSubType.HybridMutualFund => CapitalGainAssetType.EquityMutualFund,
        CapitalGainSubType.DebtMutualFund
            or CapitalGainSubType.InternationalFund => CapitalGainAssetType.DebtMutualFund,

        // Real estate.
        CapitalGainSubType.ResidentialHouse
            or CapitalGainSubType.CommercialProperty
            or CapitalGainSubType.Plot => CapitalGainAssetType.ImmovableProperty,
        CapitalGainSubType.AgriculturalLand => CapitalGainAssetType.AgriculturalLand,

        // Gold & precious.
        CapitalGainSubType.PhysicalGold
            or CapitalGainSubType.GoldEtf
            or CapitalGainSubType.OtherBullion => CapitalGainAssetType.Gold,
        CapitalGainSubType.SovereignGoldBond => CapitalGainAssetType.Bonds,
        CapitalGainSubType.Jewellery => CapitalGainAssetType.Jewellery,

        // Bonds & securities.
        CapitalGainSubType.ListedBond
            or CapitalGainSubType.Debenture
            or CapitalGainSubType.GovernmentSecurity
            or CapitalGainSubType.TaxFreeBond => CapitalGainAssetType.Bonds,

        // Virtual digital assets.
        CapitalGainSubType.Crypto or CapitalGainSubType.Nft => CapitalGainAssetType.CryptoVda,

        // Foreign equity — no STT, taxed like unlisted (s.112), flagged foreign for Schedule FA.
        CapitalGainSubType.ForeignShare
            or CapitalGainSubType.UsStock
            or CapitalGainSubType.ForeignEtf
            or CapitalGainSubType.ForeignRsu
            or CapitalGainSubType.AdrGdr => CapitalGainAssetType.UnlistedShares,

        // Other capital assets.
        _ => CapitalGainAssetType.Other,
    };

    /// <summary>True for sub-types held outside India (drive Schedule FA / FSI / TR; no STT, no 111A/112A).</summary>
    public static bool IsForeign(CapitalGainSubType subType) => subType is
        CapitalGainSubType.ForeignShare
        or CapitalGainSubType.UsStock
        or CapitalGainSubType.ForeignEtf
        or CapitalGainSubType.ForeignRsu
        or CapitalGainSubType.AdrGdr;

    /// <summary>True when STT is ordinarily paid on the sub-type's transfer (gates 111A/112A eligibility).</summary>
    public static bool SttTypicallyPaid(CapitalGainSubType subType) => CategoryOf(subType) is
        CapitalGainAssetType.ListedEquity or CapitalGainAssetType.EquityMutualFund;
}
