using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TallyG.Tax.Domain.Common;

namespace TallyG.Tax.Api.Modules.Documents;

/// <summary>
/// Documents endpoints — the two-step pre-signed upload (Decision Log D-2), synchronous extraction
/// (Ch.5 stub), HITL review/approve, and tenant/ownership-scoped list + download.
///
/// The ":verb" sub-resource convention (Ch.4 / D-3) is used for actions:
///   POST /documents:initiate-upload
///   PUT  /documents/{id}:upload-bytes
///   POST /documents/{id}:complete
///   POST /documents/{id}/extraction:approve
///   GET  /documents/{id}:download
/// Reads use plain resource routes (GET /documents, GET /documents/{id}, GET /documents/{id}/extraction).
///
/// The two "_local-*" routes back the dev IFileStorage loopback URLs (no S3 account needed); in
/// production the client PUTs/GETs object storage directly via a real pre-signed URL.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/documents")]
public sealed class DocumentsController : ControllerBase
{
    private readonly IDocumentService _documents;

    public DocumentsController(IDocumentService documents) => _documents = documents;

    /// <summary>Begin an upload: create the document row and mint an upload URL.</summary>
    [HttpPost(":initiate-upload")]
    [ProducesResponseType(typeof(InitiateUploadResponse), StatusCodes.Status200OK)]
    public Task<InitiateUploadResponse> InitiateUpload([FromBody] InitiateUploadRequest request, CancellationToken ct)
        => _documents.InitiateUploadAsync(request, ct);

    /// <summary>
    /// Canonical upload leg: stream raw bytes for a document straight to storage.
    /// (STUB: prod clients PUT directly to the S3 pre-signed URL instead.)
    /// </summary>
    [HttpPut("{id:guid}:upload-bytes")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> UploadBytes(Guid id, CancellationToken ct)
    {
        await _documents.ReceiveBytesAsync(id, Request.Body, Request.ContentType, ct);
        return NoContent();
    }

    /// <summary>Complete the upload and run extraction synchronously; returns the document.</summary>
    [HttpPost("{id:guid}:complete")]
    [ProducesResponseType(typeof(DocumentDto), StatusCodes.Status200OK)]
    public Task<DocumentDto> Complete(Guid id, [FromBody] CompleteUploadRequest? request, CancellationToken ct)
        => _documents.CompleteAsync(id, request ?? new CompleteUploadRequest(null, null), ct);

    /// <summary>List the caller's documents (tenant + ownership scoped), newest first.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<DocumentDto>), StatusCodes.Status200OK)]
    public Task<PagedResult<DocumentDto>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Guid? returnId = null,
        [FromQuery] string? kind = null,
        [FromQuery] string? status = null,
        CancellationToken ct = default)
        => _documents.ListAsync(new DocumentListQuery(page, pageSize, returnId, kind, status), ct);

    /// <summary>Get a single document's metadata.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(DocumentDto), StatusCodes.Status200OK)]
    public Task<DocumentDto> Get(Guid id, CancellationToken ct) => _documents.GetAsync(id, ct);

    /// <summary>Get the latest extraction (parsed fields + confidence) for a document.</summary>
    [HttpGet("{id:guid}/extraction")]
    [ProducesResponseType(typeof(ExtractionDto), StatusCodes.Status200OK)]
    public Task<ExtractionDto> GetExtraction(Guid id, CancellationToken ct)
        => _documents.GetExtractionAsync(id, ct);

    /// <summary>HITL accept: verify the extraction and (optionally) map fields onto the return.</summary>
    [HttpPost("{id:guid}/extraction:approve")]
    [ProducesResponseType(typeof(ApproveExtractionResponse), StatusCodes.Status200OK)]
    public Task<ApproveExtractionResponse> ApproveExtraction(
        Guid id, [FromBody] ApproveExtractionRequest? request, CancellationToken ct)
        => _documents.ApproveExtractionAsync(id, request ?? new ApproveExtractionRequest(), ct);

    /// <summary>Stream the stored file back to the caller.</summary>
    [HttpGet("{id:guid}:download")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct)
    {
        var download = await _documents.GetDownloadAsync(id, ct);
        return File(download.Content, download.ContentType, download.FileName);
    }

    // ----------------------------------------------------------- dev storage loopback

    /// <summary>
    /// Dev loopback for the local <c>IFileStorage</c> pre-signed PUT URL
    /// (…/documents/_local-upload?key=…). Streams the body into storage. In production this route
    /// does not exist — the client PUTs the bytes straight to S3.
    /// </summary>
    [HttpPut("_local-upload")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> LocalUpload([FromQuery] string key, CancellationToken ct)
    {
        await _documents.ReceiveBytesByKeyAsync(key, Request.Body, Request.ContentType, ct);
        return NoContent();
    }

    /// <summary>
    /// Dev loopback for the local <c>IFileStorage</c> download URL (…/documents/_local-download?key=…).
    /// Resolves the key to its document, scopes access, and streams the bytes.
    /// </summary>
    [HttpGet("_local-download")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> LocalDownload([FromQuery] string key, CancellationToken ct)
    {
        var download = await _documents.GetDownloadByKeyAsync(key, ct);
        return File(download.Content, download.ContentType, download.FileName);
    }
}
