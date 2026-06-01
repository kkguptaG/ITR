using TallyG.Tax.Domain.Common;

namespace TallyG.Tax.Api.Modules.Documents;

/// <summary>
/// Application service for the Documents module: the two-step pre-signed upload (Decision Log D-2),
/// synchronous extraction (Ch.5 stub), the HITL review/approve step, and tenant-scoped listing +
/// download. Auto-registered scoped by Scrutor (DocumentService : IDocumentService).
/// </summary>
public interface IDocumentService
{
    /// <summary>Create a pending Document row and issue an upload URL the client PUTs bytes to.</summary>
    Task<InitiateUploadResponse> InitiateUploadAsync(InitiateUploadRequest request, CancellationToken ct = default);

    /// <summary>
    /// Persist uploaded bytes for a document (the server-side leg of the dev upload URL) and flip
    /// the document to <c>Uploaded</c>. Returns the resolved storage key.
    /// </summary>
    Task ReceiveBytesAsync(Guid documentId, Stream body, string? contentType, CancellationToken ct = default);

    /// <summary>
    /// Receive bytes addressed by an opaque storage key (the loopback URL minted by the local
    /// <c>IFileStorage</c>). Used by the dev pre-signed PUT endpoint.
    /// </summary>
    Task ReceiveBytesByKeyAsync(string storageKey, Stream body, string? contentType, CancellationToken ct = default);

    /// <summary>
    /// Complete an upload: verify the object exists, then run extraction synchronously and persist
    /// a <c>DocumentExtraction</c>. Status becomes <c>Extracted</c>, or <c>NeedsReview</c> when the
    /// aggregate confidence is below the money gate (0.92).
    /// </summary>
    Task<DocumentDto> CompleteAsync(Guid documentId, CompleteUploadRequest request, CancellationToken ct = default);

    /// <summary>List the caller's documents (tenant + ownership scoped), most recent first.</summary>
    Task<PagedResult<DocumentDto>> ListAsync(DocumentListQuery query, CancellationToken ct = default);

    /// <summary>Get a single document the caller is allowed to see.</summary>
    Task<DocumentDto> GetAsync(Guid documentId, CancellationToken ct = default);

    /// <summary>Get the latest extraction for a document (404 if extraction has not run).</summary>
    Task<ExtractionDto> GetExtractionAsync(Guid documentId, CancellationToken ct = default);

    /// <summary>
    /// HITL accept: apply any reviewer overrides, mark the extraction reviewed/verified, and
    /// (optionally) project the verified fields onto the linked return's income sources / deductions.
    /// </summary>
    Task<ApproveExtractionResponse> ApproveExtractionAsync(
        Guid documentId, ApproveExtractionRequest request, CancellationToken ct = default);

    /// <summary>
    /// Resolve a document to a streamable payload (bytes + content type + filename) for download.
    /// Enforces tenant/ownership scoping before any storage read.
    /// </summary>
    Task<DocumentDownload> GetDownloadAsync(Guid documentId, CancellationToken ct = default);

    /// <summary>
    /// Resolve a download by opaque storage key (the loopback URL minted by the local
    /// <c>IFileStorage</c>). Scopes access via the document the key resolves to.
    /// </summary>
    Task<DocumentDownload> GetDownloadByKeyAsync(string storageKey, CancellationToken ct = default);
}

/// <summary>Filters for the document list endpoint.</summary>
public sealed record DocumentListQuery(int Page, int PageSize, Guid? ReturnId, string? Kind, string? Status);

/// <summary>A resolved download payload streamed back to the client.</summary>
public sealed record DocumentDownload(byte[] Content, string ContentType, string FileName);
