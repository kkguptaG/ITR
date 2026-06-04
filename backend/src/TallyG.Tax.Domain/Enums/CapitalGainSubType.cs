namespace TallyG.Tax.Domain.Enums;

/// <summary>
/// Fine-grained capital-asset sub-type (Ch.3 §3.6) — the rich, user-facing taxonomy behind the 8
/// category cards. Optional: when set it MAPS to a tax-behaviour <see cref="CapitalGainAssetType"/>
/// via <see cref="TaxEngine.CapitalGainTaxonomy"/>, so the engine keeps routing off the broad category
/// while the sub-type drives UI guidance, schedule nuance and (later) special-case treatment
/// (buyback 115QA, SGB maturity exemption, slump sale 50B — refined in P7). Grouped by hundreds for
/// extensibility (new sub-types slot into a group without renumbering).
/// </summary>
public enum CapitalGainSubType
{
    // Equity (0–19)
    ListedShare = 0,
    UnlistedShare = 1,
    IpoShare = 2,
    EsopShare = 3,
    RsuShare = 4,
    BonusShare = 5,
    RightsShare = 6,
    Buyback = 7,

    // Mutual funds (20–39)
    EquityMutualFund = 20,
    DebtMutualFund = 21,
    HybridMutualFund = 22,
    InternationalFund = 23,

    // Real estate (40–59)
    ResidentialHouse = 40,
    CommercialProperty = 41,
    Plot = 42,
    AgriculturalLand = 43,

    // Gold & precious (60–79)
    PhysicalGold = 60,
    GoldEtf = 61,
    SovereignGoldBond = 62,
    Jewellery = 63,
    OtherBullion = 64,

    // Bonds & securities (80–99)
    ListedBond = 80,
    Debenture = 81,
    GovernmentSecurity = 82,
    TaxFreeBond = 83,

    // Virtual digital assets (100–119)
    Crypto = 100,
    Nft = 101,

    // Foreign assets (120–139)
    ForeignShare = 120,
    UsStock = 121,
    ForeignEtf = 122,
    ForeignRsu = 123,
    AdrGdr = 124,

    // Other (140+)
    Goodwill = 140,
    IntangibleAsset = 141,
    ArtCollectible = 142,
    Vehicle = 143,
    IpRights = 144,
    SlumpSale = 145,
    Other = 199
}
