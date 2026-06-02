using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TallyG.Tax.Api.Modules.ExemptIncomes;

/// <summary>
/// Exempt-income items for Schedule EI (ITR-2/3). Owner-scoped in the service.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/returns/{returnId:guid}/exempt-income")]
public sealed class ExemptIncomeController : ControllerBase
{
    private readonly IExemptIncomeService _svc;

    public ExemptIncomeController(IExemptIncomeService svc) => _svc = svc;

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ExemptIncomeDto>), StatusCodes.Status200OK)]
    public Task<IReadOnlyList<ExemptIncomeDto>> List([FromRoute] Guid returnId, CancellationToken ct)
        => _svc.ListAsync(returnId, ct);

    [HttpPost]
    [ProducesResponseType(typeof(ExemptIncomeDto), StatusCodes.Status200OK)]
    public Task<ExemptIncomeDto> Add([FromRoute] Guid returnId, [FromBody] UpsertExemptIncomeRequest request, CancellationToken ct)
        => _svc.AddAsync(returnId, request, ct);

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete([FromRoute] Guid returnId, [FromRoute] Guid id, CancellationToken ct)
    {
        await _svc.DeleteAsync(returnId, id, ct);
        return NoContent();
    }
}
