using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TallyG.Tax.Api.Modules.ClubbedIncomes;

/// <summary>
/// Clubbed income of specified persons for Schedule SPI (ITR-2/3). Owner-scoped in the service.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/returns/{returnId:guid}/clubbed-income")]
public sealed class ClubbedIncomeController : ControllerBase
{
    private readonly IClubbedIncomeService _svc;

    public ClubbedIncomeController(IClubbedIncomeService svc) => _svc = svc;

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ClubbedIncomeDto>), StatusCodes.Status200OK)]
    public Task<IReadOnlyList<ClubbedIncomeDto>> List([FromRoute] Guid returnId, CancellationToken ct)
        => _svc.ListAsync(returnId, ct);

    [HttpPost]
    [ProducesResponseType(typeof(ClubbedIncomeDto), StatusCodes.Status200OK)]
    public Task<ClubbedIncomeDto> Add([FromRoute] Guid returnId, [FromBody] UpsertClubbedIncomeRequest request, CancellationToken ct)
        => _svc.AddAsync(returnId, request, ct);

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete([FromRoute] Guid returnId, [FromRoute] Guid id, CancellationToken ct)
    {
        await _svc.DeleteAsync(returnId, id, ct);
        return NoContent();
    }
}
