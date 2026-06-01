using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TallyG.Tax.Api.Modules.Reporting;

/// <summary>
/// Take-away report downloads for a return (docs 09): the ITR-V acknowledgment and the computation
/// worksheet. User-scoped — the service enforces owner-or-operator access. The PDF bytes are
/// streamed directly (Content-Disposition: attachment); a copy is also persisted to the vault.
/// </summary>
[ApiController]
[Route("api/v1/returns")]
[Authorize]
public sealed class ReturnReportsController : ControllerBase
{
    private readonly IReportingService _reporting;

    public ReturnReportsController(IReportingService reporting) => _reporting = reporting;

    /// <summary>Download the ITR-V acknowledgment PDF for a filed return.</summary>
    [HttpGet("{id:guid}/acknowledgment")]
    [Produces("application/pdf")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Acknowledgment([FromRoute] Guid id, CancellationToken ct)
    {
        var file = await _reporting.GetAcknowledgmentAsync(id, ct);
        return File(file.Content, file.ContentType, file.FileName);
    }

    /// <summary>Download the computation worksheet PDF for a computed return.</summary>
    [HttpGet("{id:guid}/computation")]
    [Produces("application/pdf")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Computation([FromRoute] Guid id, CancellationToken ct)
    {
        var file = await _reporting.GetComputationAsync(id, ct);
        return File(file.Content, file.ContentType, file.FileName);
    }

    /// <summary>List the generated take-away artifacts registered against a return.</summary>
    [HttpGet("{id:guid}/documents")]
    [ProducesResponseType(typeof(IReadOnlyList<GeneratedDocumentDto>), StatusCodes.Status200OK)]
    public Task<IReadOnlyList<GeneratedDocumentDto>> Documents([FromRoute] Guid id, CancellationToken ct)
        => _reporting.ListReturnDocumentsAsync(id, ct);
}
