using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TallyG.Tax.Api.Modules.AssetsLiabilities;

/// <summary>
/// Immovable properties for Schedule AL's ImmovableDetails list (ITR-2/3). Owner-scoped in the service.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/returns/{returnId:guid}/immovable-assets")]
public sealed class ImmovableAssetsController : ControllerBase
{
    private readonly IImmovableAssetsService _svc;

    public ImmovableAssetsController(IImmovableAssetsService svc) => _svc = svc;

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ImmovablePropertyAlDto>), StatusCodes.Status200OK)]
    public Task<IReadOnlyList<ImmovablePropertyAlDto>> List([FromRoute] Guid returnId, CancellationToken ct)
        => _svc.ListAsync(returnId, ct);

    [HttpPost]
    [ProducesResponseType(typeof(ImmovablePropertyAlDto), StatusCodes.Status200OK)]
    public Task<ImmovablePropertyAlDto> Add([FromRoute] Guid returnId, [FromBody] UpsertImmovablePropertyAlRequest request, CancellationToken ct)
        => _svc.AddAsync(returnId, request, ct);

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete([FromRoute] Guid returnId, [FromRoute] Guid id, CancellationToken ct)
    {
        await _svc.DeleteAsync(returnId, id, ct);
        return NoContent();
    }
}
