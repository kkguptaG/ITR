using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TallyG.Tax.Api.Modules.ForeignSourceIncomes;

/// <summary>
/// Foreign-source income + double-taxation relief for Schedule FSI / TR1 (ITR-2/3). Owner-scoped in the service.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/returns/{returnId:guid}/foreign-source-income")]
public sealed class ForeignSourceIncomeController : ControllerBase
{
    private readonly IForeignSourceIncomeService _svc;

    public ForeignSourceIncomeController(IForeignSourceIncomeService svc) => _svc = svc;

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ForeignSourceIncomeDto>), StatusCodes.Status200OK)]
    public Task<IReadOnlyList<ForeignSourceIncomeDto>> List([FromRoute] Guid returnId, CancellationToken ct)
        => _svc.ListAsync(returnId, ct);

    [HttpPost]
    [ProducesResponseType(typeof(ForeignSourceIncomeDto), StatusCodes.Status200OK)]
    public Task<ForeignSourceIncomeDto> Add([FromRoute] Guid returnId, [FromBody] UpsertForeignSourceIncomeRequest request, CancellationToken ct)
        => _svc.AddAsync(returnId, request, ct);

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete([FromRoute] Guid returnId, [FromRoute] Guid id, CancellationToken ct)
    {
        await _svc.DeleteAsync(returnId, id, ct);
        return NoContent();
    }
}
