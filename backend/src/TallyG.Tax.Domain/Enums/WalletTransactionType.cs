namespace TallyG.Tax.Domain.Enums;

/// <summary>Direction/category of a wallet ledger entry.</summary>
public enum WalletTransactionType
{
    Credit = 0,
    Debit = 1,
    Refund = 2,
    ReferralBonus = 3,
    Cashback = 4
}
