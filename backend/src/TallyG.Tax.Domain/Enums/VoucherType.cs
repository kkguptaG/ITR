namespace TallyG.Tax.Domain.Enums;

/// <summary>
/// Accounting voucher type, in Tally's vocabulary. A money-in bank line posts a
/// <see cref="Receipt"/> (Dr Bank, Cr counter-ledger); a money-out line posts a
/// <see cref="Payment"/> (Dr counter-ledger, Cr Bank). <see cref="Contra"/> covers
/// bank-to-bank / bank-to-cash transfers; <see cref="Journal"/> is the general-purpose
/// adjustment voucher (reserved for non-bank entries).
/// </summary>
public enum VoucherType
{
    Receipt = 0,
    Payment = 1,
    Contra = 2,
    Journal = 3
}
