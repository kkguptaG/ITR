using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TallyG.Tax.Api.Modules.PassThroughIncomes;

/// <summary>
/// Pass-through income (business trust / investment fund) for Schedule PTI (ITR-2/3). Owner-scoped in the service.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/returns/{returnId:guid}/pass-through-income")]
public sealed class PassThroughIncomeController : ControllerBase
{
    private readonly IPassThroughIncomeService _svc;

    public PassThroughIncomeController(IPassThroughIncomeService svc) => _svc = svc;

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PassThroughIncomeDto>), StatusCodes.Status200OK)]
    public Task<IReadOnlyList<PassThroughIncomeDto>> List([FromRoute] Guid returnId, CancellationToken ct)
        => _svc.ListAsync(returnId, ct);

    [HttpPost]
    [ProducesResponseType(typeof(PassThroughIncomeDto), StatusCodes.Status200OK)]
    public Task<PassThroughIncomeDto> Add([FromRoute] Guid returnId, [FromBody] UpsertPassThroughIncomeRequest request, CancellationToken ct)
        => _svc.AddAsync(returnId, request, ct);

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete([FromRoute] Guid returnId, [FromRoute] Guid id, CancellationToken ct)
    {
        await _svc.DeleteAsync(returnId, id, ct);
        return NoContent();
    }
}
