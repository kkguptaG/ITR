namespace TallyG.Tax.Domain.Enums;

/// <summary>
/// The accounting nature of a ledger, derived from its <see cref="LedgerGroup"/>. Determines the
/// normal balance side: assets and expenses are debit-normal; liabilities, income and equity are
/// credit-normal (see <c>Accounting.LedgerGroupMeta</c>).
/// </summary>
public enum LedgerNature
{
    Asset = 0,
    Liability = 1,
    Income = 2,
    Expense = 3,
    Equity = 4
}
