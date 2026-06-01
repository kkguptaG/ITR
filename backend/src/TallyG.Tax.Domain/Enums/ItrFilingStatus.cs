namespace TallyG.Tax.Domain.Enums;

/// <summary>
/// Lifecycle of a generated ITR JSON artifact (offline-filing model).
/// Generated → produced but not yet (re)validated · Valid → passed validation, ready to upload
/// on the Income Tax portal · Invalid → validation found blocking errors.
/// </summary>
public enum ItrFilingStatus
{
    Generated,
    Valid,
    Invalid
}
