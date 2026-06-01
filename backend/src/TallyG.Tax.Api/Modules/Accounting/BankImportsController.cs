using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TallyG.Tax.Domain.Common;

namespace TallyG.Tax.Api.Modules.Accounting;

/// <summary>
/// Bank-statement import endpoints. The upload is a single raw-body POST (the file bytes are the
/// request body; <c>fileName</c> / <c>bankLedgerId</c> ride on the query string and the file's
/// content type is the request Content-Type) — it stores, parses and matches in one call and returns
/// the lines + suggestions for review. The ":post" sub-resource commits reviewed lines to the books.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/accounting/bank-imports")]
public sealed class BankImportsController : ControllerBase
{
    private readonly IBankImportService _imports;

    public BankImportsController(IBankImportService imports) => _imports = imports;

    /// <summary>Upload + parse + match a statement in one call; returns the lines with suggestions.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(BankImportDetailDto), StatusCodes.Status200OK)]
    public Task<BankImportDetailDto> Upload(
        [FromQuery] string fileName,
        [FromQuery] Guid? bankLedgerId,
        CancellationToken ct)
        => _imports.UploadAsync(Request.Body, fileName, Request.ContentType, bankLedgerId, ct);

    /// <summary>List the caller's statement imports, newest first.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<BankImportDto>), StatusCodes.Status200OK)]
    public Task<PagedResult<BankImportDto>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => _imports.ListAsync(page, pageSize, ct);

    /// <summary>Get an import with its parsed lines + matcher suggestions (the review payload).</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(BankImportDetailDto), StatusCodes.Status200OK)]
    public Task<BankImportDetailDto> Get(Guid id, CancellationToken ct) => _imports.GetAsync(id, ct);

    /// <summary>Commit reviewed lines: create adopted ledgers and post their double-entry vouchers.</summary>
    [HttpPost("{id:guid}:post")]
    [ProducesResponseType(typeof(PostImportResponse), StatusCodes.Status200OK)]
    public Task<PostImportResponse> Post(Guid id, [FromBody] PostImportRequest? request, CancellationToken ct)
        => _imports.PostAsync(id, request ?? new PostImportRequest(), ct);

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _imports.DeleteAsync(id, ct);
        return NoContent();
    }
}
