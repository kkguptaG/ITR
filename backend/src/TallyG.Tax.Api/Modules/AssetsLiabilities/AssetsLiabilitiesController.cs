using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TallyG.Tax.Api.Modules.AssetsLiabilities;

/// <summary>
/// The return's Schedule AL declaration (movable assets + liabilities, required when total income > ₹50L).
/// GET returns the declaration (zeros if none yet); PUT upserts it. Owner-scoped in the service.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/returns/{returnId:guid}/assets-liabilities")]
public sealed class AssetsLiabilitiesController : ControllerBase
{
    private readonly IAssetsLiabilitiesService _svc;

    public AssetsLiabilitiesController(IAssetsLiabilitiesService svc) => _svc = svc;

    [HttpGet]
    [ProducesResponseType(typeof(AssetsLiabilitiesDto), StatusCodes.Status200OK)]
    public Task<AssetsLiabilitiesDto> Get([FromRoute] Guid returnId, CancellationToken ct)
        => _svc.GetAsync(returnId, ct);

    [HttpPut]
    [ProducesResponseType(typeof(AssetsLiabilitiesDto), StatusCodes.Status200OK)]
    public Task<AssetsLiabilitiesDto> Upsert([FromRoute] Guid returnId, [FromBody] UpsertAssetsLiabilitiesRequest request, CancellationToken ct)
        => _svc.UpsertAsync(returnId, request, ct);
}
