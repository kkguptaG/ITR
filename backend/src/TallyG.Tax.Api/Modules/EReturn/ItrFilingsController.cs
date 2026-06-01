using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TallyG.Tax.Domain.Common;

namespace TallyG.Tax.Api.Modules.EReturn;

/// <summary>
/// Offline-filing ITR JSON endpoints (pre-ERI): generate the ITD-format JSON for a return, validate
/// it, list the saved "ready to file" artifacts, and download the JSON to upload on the Income Tax
/// portal after login. Owner-scoped (the service enforces it). The ":verb" convention matches the
/// rest of the API; downloads stream as application/json attachments.
/// </summary>
[ApiController]
[Route("api/v1")]
[Authorize]
public sealed class ItrFilingsController : ControllerBase
{
    private readonly IItrJsonService _svc;

    public ItrFilingsController(IItrJsonService svc) => _svc = svc;

    /// <summary>Generate (and auto-validate) the ITR JSON for a return; saves/replaces its artifact.</summary>
    [HttpPost("returns/{id:guid}/itr-json:generate")]
    [ProducesResponseType(typeof(GenerateItrJsonResponse), StatusCodes.Status200OK)]
    public Task<GenerateItrJsonResponse> Generate([FromRoute] Guid id, CancellationToken ct)
        => _svc.GenerateAsync(id, ct);

    /// <summary>List the saved ITR JSON artifact(s) for one return.</summary>
    [HttpGet("returns/{id:guid}/itr-json")]
    [ProducesResponseType(typeof(IReadOnlyList<ItrJsonArtifactDto>), StatusCodes.Status200OK)]
    public Task<IReadOnlyList<ItrJsonArtifactDto>> ListForReturn([FromRoute] Guid id, CancellationToken ct)
        => _svc.ListForReturnAsync(id, ct);

    /// <summary>The user's full "ready to file" list across all returns (paged).</summary>
    [HttpGet("itr-json")]
    [ProducesResponseType(typeof(PagedResult<ItrJsonArtifactDto>), StatusCodes.Status200OK)]
    public Task<PagedResult<ItrJsonArtifactDto>> ListMine(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => _svc.ListMineAsync(page, pageSize, ct);

    /// <summary>Artifact metadata (status + counts) for one generated JSON.</summary>
    [HttpGet("itr-json/{fileId:guid}")]
    [ProducesResponseType(typeof(ItrJsonArtifactDto), StatusCodes.Status200OK)]
    public Task<ItrJsonArtifactDto> Get([FromRoute] Guid fileId, CancellationToken ct)
        => _svc.GetAsync(fileId, ct);

    /// <summary>Re-run validation against the stored JSON and refresh its status.</summary>
    [HttpPost("itr-json/{fileId:guid}:validate")]
    [ProducesResponseType(typeof(ValidationReportDto), StatusCodes.Status200OK)]
    public Task<ValidationReportDto> Validate([FromRoute] Guid fileId, CancellationToken ct)
        => _svc.ValidateAsync(fileId, ct);

    /// <summary>The last stored validation report (issues + suggestions) for an artifact — no re-run.</summary>
    [HttpGet("itr-json/{fileId:guid}/report")]
    [ProducesResponseType(typeof(ValidationReportDto), StatusCodes.Status200OK)]
    public Task<ValidationReportDto> GetReport([FromRoute] Guid fileId, CancellationToken ct)
        => _svc.GetReportAsync(fileId, ct);

    /// <summary>Download the ITR JSON file to upload on the Income Tax portal.</summary>
    [HttpGet("itr-json/{fileId:guid}:download")]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Download([FromRoute] Guid fileId, CancellationToken ct)
    {
        var file = await _svc.DownloadAsync(fileId, ct);
        return File(file.Content, "application/json", file.FileName);
    }
}
