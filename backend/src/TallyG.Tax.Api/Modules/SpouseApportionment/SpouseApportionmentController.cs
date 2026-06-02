using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TallyG.Tax.Api.Modules.SpouseApportionment;

/// <summary>
/// Portuguese-Civil-Code spouse apportionment for Schedule 5A (ITR-2/3). One record per return.
/// Owner-scoped in the service.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/returns/{returnId:guid}/spouse-apportionment")]
public sealed class SpouseApportionmentController : ControllerBase
{
    private readonly ISpouseApportionmentService _svc;

    public SpouseApportionmentController(ISpouseApportionmentService svc) => _svc = svc;

    [HttpGet]
    [ProducesResponseType(typeof(SpouseApportionmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Get([FromRoute] Guid returnId, CancellationToken ct)
    {
        var dto = await _svc.GetAsync(returnId, ct);
        return dto is null ? NoContent() : Ok(dto);
    }

    [HttpPut]
    [ProducesResponseType(typeof(SpouseApportionmentDto), StatusCodes.Status200OK)]
    public Task<SpouseApportionmentDto> Put([FromRoute] Guid returnId, [FromBody] UpsertSpouseApportionmentRequest request, CancellationToken ct)
        => _svc.UpsertAsync(returnId, request, ct);

    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete([FromRoute] Guid returnId, CancellationToken ct)
    {
        await _svc.DeleteAsync(returnId, ct);
        return NoContent();
    }
}
