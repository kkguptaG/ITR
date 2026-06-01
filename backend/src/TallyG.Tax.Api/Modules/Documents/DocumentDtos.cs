// Documents module — request/response DTOs.
// These records are the public Documents contract (two-step pre-signed upload, Decision Log D-2)
// consumed by the frontend. JSON is camelCase on the wire (ASP.NET Core default), mapping to
// these PascalCase records. See docs/architecture/05-ai-and-documents.md and 10-ai-ocr-persistence.md.

using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Api.Modules.Documents;

// --- :initiate-upload ---

/// <summary>
/// POST /documents:initiate-upload body. <paramref name="Kind"/> is a <see cref="DocumentKind"/>
/// name (e.g. "Form16", "Form26AS", "AIS", "BankStatement"); <paramref name="ReturnId"/> is optional —
/// a document may be uploaded before it is linked to a return.
/// </summary>
public sealed record InitiateUploadRequest(
    string Kind,
    string FileName,
    string ContentType,
    Guid? ReturnId);

/// <summary>
/// POST /documents:initiate-upload response. The client PUTs the raw bytes to
/// <see cref="UploadUrl"/> (in dev this is a loopback endpoint that streams to
/// <c>IFileStorage</c>; STUB: prod returns an S3 pre-signed PUT URL), then calls
/// <c>POST /documents/{id}:complete</c>. <see cref="UploadHeaders"/> must be replayed on the PUT.
/// </summary>
public sealed record InitiateUploadResponse(
    Guid DocumentId,
    string UploadUrl,
    string UploadMethod,
    IReadOnlyDictionary<string, string> UploadHeaders,
    DateTimeOffset ExpiresAt);

// --- :complete ---

/// <summary>
/// POST /documents/{id}:complete body. <paramref name="ETag"/> / <paramref name="Sha256"/> are
/// optional integrity hints the client may pass back from the upload (mirrors the S3 ETag flow).
/// </summary>
public sealed record CompleteUploadRequest(string? ETag, string? Sha256);

// --- document views ---

/// <summary>A document metadata row (the bytes live in object storage).</summary>
public sealed record DocumentDto(
    Guid Id,
    Guid? ReturnId,
    string Kind,
    string FileName,
    string ContentType,
    long SizeBytes,
    string Status,
    string? Sha256,
    bool HasExtraction,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>One extracted field with provenance + confidence (flattened from FieldsJson).</summary>
public sealed record ExtractedFieldDto(
    string Key,
    string? Value,
    decimal? Confidence);

/// <summary>The extraction result for a document, including the parsed field map.</summary>
public sealed record ExtractionDto(
    Guid Id,
    Guid DocumentId,
    string DocClass,
    string Status,
    decimal? ConfidenceScore,
    string FieldsJson,
    IReadOnlyList<ExtractedFieldDto> Fields,
    bool NeedsReview,
    Guid? ReviewedByUserId,
    DateTimeOffset? ReviewedAt,
    DateTimeOffset CreatedAt);

// --- extraction:approve ---

/// <summary>
/// POST /documents/{id}/extraction:approve body. When <paramref name="MapToReturn"/> is true and the
/// document is linked to a return, the verified fields are projected onto the return's income
/// sources / deductions. <paramref name="FieldOverrides"/> lets the reviewer correct values
/// (HITL) before acceptance — keys must match the canonical extraction field keys.
/// </summary>
public sealed record ApproveExtractionRequest(
    bool MapToReturn = true,
    IReadOnlyDictionary<string, string>? FieldOverrides = null);

/// <summary>Result of approving an extraction: the verified extraction + what was mapped onto the return.</summary>
public sealed record ApproveExtractionResponse(
    ExtractionDto Extraction,
    int IncomeSourcesUpserted,
    int DeductionsUpserted);
