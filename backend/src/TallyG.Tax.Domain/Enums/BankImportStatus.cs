namespace TallyG.Tax.Domain.Enums;

/// <summary>
/// Lifecycle of an uploaded bank statement (<see cref="Entities.BankStatementImport"/>):
/// bytes received → parsed into lines → matcher ran → (some lines need a human decision) →
/// vouchers posted to the books. <see cref="Failed"/> is set when parsing yields no usable rows.
/// </summary>
public enum BankImportStatus
{
    Uploaded = 0,
    Parsing = 1,
    Parsed = 2,
    NeedsReview = 3,
    Posted = 4,
    Failed = 5
}
