namespace TallyG.Tax.Domain.Enums;

/// <summary>
/// Per-line state of a bank statement row (<see cref="Entities.BankStatementLine"/>) as it moves
/// through matching and review. The matcher leaves every line <see cref="Suggested"/>; the user
/// confirms, edits or skips each; on commit, confirmed lines become <see cref="Posted"/> with a
/// linked voucher.
/// </summary>
public enum BankLineStatus
{
    Suggested = 0,
    Confirmed = 1,
    Skipped = 2,
    Posted = 3
}
