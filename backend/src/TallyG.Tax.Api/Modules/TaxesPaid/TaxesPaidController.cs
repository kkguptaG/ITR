using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TallyG.Tax.Api.Modules.TaxesPaid;

/// <summary>
/// Deductor-wise TDS + self-paid challans for one return → the ITR's TDS schedules and Schedule IT.
/// All routes are owner/tenant-scoped in the service; totals roll up onto the return's prepaid taxes.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/returns/{returnId:guid}/taxes-paid")]
public sealed class TaxesPaidController : ControllerBase
{
    private readonly ITaxesPaidService _svc;

    public TaxesPaidController(ITaxesPaidService svc) => _svc = svc;

    [HttpGet]
    [ProducesResponseType(typeof(TaxesPaidSummaryDto), StatusCodes.Status200OK)]
    public Task<TaxesPaidSummaryDto> Get([FromRoute] Guid returnId, CancellationToken ct)
        => _svc.GetAsync(returnId, ct);

    [HttpPost("tds")]
    [ProducesResponseType(typeof(TdsEntryDto), StatusCodes.Status200OK)]
    public Task<TdsEntryDto> AddTds([FromRoute] Guid returnId, [FromBody] UpsertTdsEntryRequest request, CancellationToken ct)
        => _svc.AddTdsAsync(returnId, request, ct);

    [HttpDelete("tds/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteTds([FromRoute] Guid returnId, [FromRoute] Guid id, CancellationToken ct)
    {
        await _svc.DeleteTdsAsync(returnId, id, ct);
        return NoContent();
    }

    [HttpPost("challans")]
    [ProducesResponseType(typeof(ChallanDto), StatusCodes.Status200OK)]
    public Task<ChallanDto> AddChallan([FromRoute] Guid returnId, [FromBody] UpsertChallanRequest request, CancellationToken ct)
        => _svc.AddChallanAsync(returnId, request, ct);

    [HttpDelete("challans/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteChallan([FromRoute] Guid returnId, [FromRoute] Guid id, CancellationToken ct)
    {
        await _svc.DeleteChallanAsync(returnId, id, ct);
        return NoContent();
    }
}
