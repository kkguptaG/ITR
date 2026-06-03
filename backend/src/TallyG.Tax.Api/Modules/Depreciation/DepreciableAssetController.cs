using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TallyG.Tax.Api.Modules.Depreciation;

/// <summary>
/// Depreciable plant &amp; machinery blocks for Schedule DPM (ITR-3). Owner-scoped in the service.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/returns/{returnId:guid}/depreciable-assets")]
public sealed class DepreciableAssetController : ControllerBase
{
    private readonly IDepreciableAssetService _svc;

    public DepreciableAssetController(IDepreciableAssetService svc) => _svc = svc;

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<DepreciableAssetDto>), StatusCodes.Status200OK)]
    public Task<IReadOnlyList<DepreciableAssetDto>> List([FromRoute] Guid returnId, CancellationToken ct)
        => _svc.ListAsync(returnId, ct);

    [HttpPost]
    [ProducesResponseType(typeof(DepreciableAssetDto), StatusCodes.Status200OK)]
    public Task<DepreciableAssetDto> Add([FromRoute] Guid returnId, [FromBody] UpsertDepreciableAssetRequest request, CancellationToken ct)
        => _svc.AddAsync(returnId, request, ct);

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete([FromRoute] Guid returnId, [FromRoute] Guid id, CancellationToken ct)
    {
        await _svc.DeleteAsync(returnId, id, ct);
        return NoContent();
    }
}
