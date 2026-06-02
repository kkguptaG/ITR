namespace TallyG.Tax.Api.Modules.Reconciliation;

/// <summary>
/// One reconciliation line: a figure as fed in the return vs as reported by the department (AIS/26AS),
/// with a status. "under_reported" is the one that risks a notice (the return shows less than the dept).
/// </summary>
public sealed record ReconLineDto(
    string Category,
    string Label,
    decimal InReturn,
    decimal InSource,
    string Source,        // "AIS" | "26AS"
    string Status,        // "matched" | "under_reported" | "over_reported" | "only_in_source"
    string Note);

/// <summary>
/// The pre-filing reconciliation of a return against the latest uploaded AIS + Form 26AS extractions.
/// <see cref="HasSources"/> is false when neither has been uploaded/extracted for the return.
/// </summary>
public sealed record ReconciliationReportDto(
    bool HasSources,
    IReadOnlyList<ReconLineDto> Lines,
    int MismatchCount,
    int UnderReportedCount,
    string Notice);
