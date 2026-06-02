using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TallyG.Tax.Api.Modules.AssetsLiabilities;

/// <summary>
/// Interests in a firm / AOP for Schedule AL's InterestHeldInaAsset list (ITR-3). Owner-scoped in the service.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/returns/{returnId:guid}/firm-interests")]
public sealed class FirmInterestsController : ControllerBase
{
    private readonly IFirmInterestsService _svc;

    public FirmInterestsController(IFirmInterestsService svc) => _svc = svc;

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<FirmInterestAlDto>), StatusCodes.Status200OK)]
    public Task<IReadOnlyList<FirmInterestAlDto>> List([FromRoute] Guid returnId, CancellationToken ct)
        => _svc.ListAsync(returnId, ct);

    [HttpPost]
    [ProducesResponseType(typeof(FirmInterestAlDto), StatusCodes.Status200OK)]
    public Task<FirmInterestAlDto> Add([FromRoute] Guid returnId, [FromBody] UpsertFirmInterestAlRequest request, CancellationToken ct)
        => _svc.AddAsync(returnId, request, ct);

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete([FromRoute] Guid returnId, [FromRoute] Guid id, CancellationToken ct)
    {
        await _svc.DeleteAsync(returnId, id, ct);
        return NoContent();
    }
}
