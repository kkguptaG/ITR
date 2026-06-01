namespace TallyG.Tax.Domain.Enums;

/// <summary>Asset class driving capital-gain tax rates (Ch.3 §3.6.1).</summary>
public enum CapitalGainAssetType
{
    ListedEquity = 0,
    EquityMutualFund = 1,
    DebtMutualFund = 2,
    UnlistedShares = 3,
    ImmovableProperty = 4,
    Bonds = 5,
    Gold = 6,
    CryptoVda = 7,
    Other = 99
}
