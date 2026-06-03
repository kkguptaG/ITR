using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TallyG.Tax.Api.Modules.Depreciation;

/// <summary>
/// Brought-forward unabsorbed depreciation rows for Schedule UD (ITR-3). Owner-scoped in the service.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/returns/{returnId:guid}/unabsorbed-depreciation")]
public sealed class UnabsorbedDepreciationController : ControllerBase
{
    private readonly IUnabsorbedDepreciationService _svc;

    public UnabsorbedDepreciationController(IUnabsorbedDepreciationService svc) => _svc = svc;

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<UnabsorbedDepreciationDto>), StatusCodes.Status200OK)]
    public Task<IReadOnlyList<UnabsorbedDepreciationDto>> List([FromRoute] Guid returnId, CancellationToken ct)
        => _svc.ListAsync(returnId, ct);

    [HttpPost]
    [ProducesResponseType(typeof(UnabsorbedDepreciationDto), StatusCodes.Status200OK)]
    public Task<UnabsorbedDepreciationDto> Add([FromRoute] Guid returnId, [FromBody] UpsertUnabsorbedDepreciationRequest request, CancellationToken ct)
        => _svc.AddAsync(returnId, request, ct);

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete([FromRoute] Guid returnId, [FromRoute] Guid id, CancellationToken ct)
    {
        await _svc.DeleteAsync(returnId, id, ct);
        return NoContent();
    }
}
