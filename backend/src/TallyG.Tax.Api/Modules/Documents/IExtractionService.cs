using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Api.Modules.Documents;

/// <summary>
/// Turns an uploaded document's bytes into a structured, confidence-scored field map.
/// In production this fans out to layout OCR (Textract / Azure DI) then a structured
/// extractor (Ch.5); in the runnable core it is a deterministic mock keyed on
/// <see cref="DocumentKind"/>. Auto-registered scoped by Scrutor (ExtractionService :
/// IExtractionService).
/// </summary>
public interface IExtractionService
{
    /// <summary>
    /// Extract canonical fields for a document. The implementation is pure with respect to the
    /// inputs (no persistence) so the caller owns the <c>DocumentExtraction</c> row.
    /// </summary>
    Task<ExtractionResult> ExtractAsync(ExtractionInput input, CancellationToken ct = default);
}

/// <summary>Inputs to an extraction run.</summary>
public sealed record ExtractionInput(
    Guid DocumentId,
    DocumentKind Kind,
    string FileName,
    string ContentType,
    byte[]? Content);

/// <summary>One extracted field: a canonical key, a string value, and a per-field confidence in [0,1].</summary>
public sealed record ExtractionField(string Key, string Value, decimal Confidence);

/// <summary>
/// Result of an extraction: the doc class the extractor settled on, an aggregate confidence,
/// and the per-field list. The caller persists <see cref="Fields"/> as the extraction FieldsJson
/// and gates on <see cref="AggregateConfidence"/>.
/// </summary>
public sealed record ExtractionResult(
    string DocClass,
    decimal AggregateConfidence,
    IReadOnlyList<ExtractionField> Fields);
