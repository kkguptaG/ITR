namespace TallyG.Tax.Domain.Enums;

/// <summary>
/// The side of a posting — Debit or Credit. Used both for a <see cref="Entities.VoucherEntry"/>
/// (which side of the double entry the ledger sits on) and, on a bank statement line, for the
/// direction of the bank movement (a withdrawal debits the customer's view of the bank column;
/// a deposit credits it).
/// </summary>
public enum DrCr
{
    Debit = 0,
    Credit = 1
}
