using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TallyG.Tax.Api.Modules.Accounting;

/// <summary>
/// Chart-of-accounts endpoints for a user's standalone books. Lists include the system-generated
/// " (E)" heads (filter with <c>?systemGenerated=true</c>); editing a head adopts it (clears the flag).
/// All routes are tenant/owner-scoped in the service.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/accounting/ledgers")]
public sealed class LedgersController : ControllerBase
{
    private readonly ILedgerService _ledgers;

    public LedgersController(ILedgerService ledgers) => _ledgers = ledgers;

    /// <summary>List ledgers, optionally filtered by group / generated / bank.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<LedgerDto>), StatusCodes.Status200OK)]
    public Task<IReadOnlyList<LedgerDto>> List(
        [FromQuery] string? group = null,
        [FromQuery] bool? systemGenerated = null,
        [FromQuery] bool? bank = null,
        CancellationToken ct = default)
        => _ledgers.ListAsync(group, systemGenerated, bank, ct);

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(LedgerDto), StatusCodes.Status200OK)]
    public Task<LedgerDto> Get(Guid id, CancellationToken ct) => _ledgers.GetAsync(id, ct);

    [HttpPost]
    [ProducesResponseType(typeof(LedgerDto), StatusCodes.Status200OK)]
    public Task<LedgerDto> Create([FromBody] CreateLedgerRequest request, CancellationToken ct)
        => _ledgers.CreateAsync(request, ct);

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(LedgerDto), StatusCodes.Status200OK)]
    public Task<LedgerDto> Update(Guid id, [FromBody] UpdateLedgerRequest request, CancellationToken ct)
        => _ledgers.UpdateAsync(id, request, ct);

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _ledgers.DeleteAsync(id, ct);
        return NoContent();
    }
}
