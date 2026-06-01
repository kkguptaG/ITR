namespace TallyG.Tax.Domain.Enums;

/// <summary>
/// Primary chart-of-accounts group an account head (<see cref="Entities.Ledger"/>) belongs to,
/// modelled on Tally's primary groups. The group fixes the account's <see cref="LedgerNature"/>
/// (asset/liability/income/expense/equity) and therefore whether it is debit- or credit-normal —
/// see <c>Accounting.LedgerGroupMeta</c>. <see cref="Suspense"/> is the catch-all for entries the
/// matcher could not confidently classify.
/// </summary>
public enum LedgerGroup
{
    BankAccounts = 0,
    CashInHand = 1,
    SundryDebtors = 2,
    SundryCreditors = 3,
    SalesIncome = 4,
    OtherIncome = 5,
    PurchaseAccounts = 6,
    DirectExpenses = 7,
    IndirectExpenses = 8,
    DutiesAndTaxes = 9,
    LoansAndLiabilities = 10,
    FixedAssets = 11,
    Investments = 12,
    CapitalAccount = 13,
    Suspense = 14
}
