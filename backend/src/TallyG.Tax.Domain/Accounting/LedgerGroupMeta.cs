using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Domain.Accounting;

/// <summary>
/// Static metadata about chart-of-accounts groups: the accounting <see cref="LedgerNature"/> each
/// group carries and whether that nature is debit-normal. This is the single source of truth that
/// keeps a ledger's stored nature consistent with its group and lets the voucher poster compute
/// running balances with the correct sign.
/// </summary>
public static class LedgerGroupMeta
{
    /// <summary>The accounting nature implied by a chart-of-accounts group.</summary>
    public static LedgerNature NatureOf(LedgerGroup group) => group switch
    {
        LedgerGroup.BankAccounts => LedgerNature.Asset,
        LedgerGroup.CashInHand => LedgerNature.Asset,
        LedgerGroup.SundryDebtors => LedgerNature.Asset,
        LedgerGroup.FixedAssets => LedgerNature.Asset,
        LedgerGroup.Investments => LedgerNature.Asset,

        LedgerGroup.SundryCreditors => LedgerNature.Liability,
        LedgerGroup.DutiesAndTaxes => LedgerNature.Liability,
        LedgerGroup.LoansAndLiabilities => LedgerNature.Liability,

        LedgerGroup.SalesIncome => LedgerNature.Income,
        LedgerGroup.OtherIncome => LedgerNature.Income,

        LedgerGroup.PurchaseAccounts => LedgerNature.Expense,
        LedgerGroup.DirectExpenses => LedgerNature.Expense,
        LedgerGroup.IndirectExpenses => LedgerNature.Expense,

        LedgerGroup.CapitalAccount => LedgerNature.Equity,

        // Suspense is unclassified; treat it as an asset-side holding account for balance signing.
        LedgerGroup.Suspense => LedgerNature.Asset,
        _ => LedgerNature.Asset
    };

    /// <summary>Assets and expenses increase on the debit side; everything else on the credit side.</summary>
    public static bool IsDebitNormal(LedgerNature nature)
        => nature is LedgerNature.Asset or LedgerNature.Expense;

    /// <summary>True for the bank/cash groups whose ledgers represent a real account balance.</summary>
    public static bool IsBankOrCash(LedgerGroup group)
        => group is LedgerGroup.BankAccounts or LedgerGroup.CashInHand;
}
