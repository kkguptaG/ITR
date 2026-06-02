using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TallyG.Tax.Api.Modules.ForeignAssets;

/// <summary>
/// Foreign custodial/brokerage accounts + equity/debt interests for Schedule FA (ITR-2/3). Owner-scoped.
/// Two sibling resources under the return; routes are declared per-method.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/returns/{returnId:guid}")]
public sealed class ForeignInvestmentsController : ControllerBase
{
    private readonly IForeignInvestmentsService _svc;

    public ForeignInvestmentsController(IForeignInvestmentsService svc) => _svc = svc;

    // ----------------------------------------------------------------- custodial accounts
    [HttpGet("foreign-custodial-accounts")]
    [ProducesResponseType(typeof(IReadOnlyList<ForeignCustodialAccountDto>), StatusCodes.Status200OK)]
    public Task<IReadOnlyList<ForeignCustodialAccountDto>> ListCustodial([FromRoute] Guid returnId, CancellationToken ct)
        => _svc.ListCustodialAsync(returnId, ct);

    [HttpPost("foreign-custodial-accounts")]
    [ProducesResponseType(typeof(ForeignCustodialAccountDto), StatusCodes.Status200OK)]
    public Task<ForeignCustodialAccountDto> AddCustodial([FromRoute] Guid returnId, [FromBody] UpsertForeignCustodialAccountRequest request, CancellationToken ct)
        => _svc.AddCustodialAsync(returnId, request, ct);

    [HttpDelete("foreign-custodial-accounts/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteCustodial([FromRoute] Guid returnId, [FromRoute] Guid id, CancellationToken ct)
    {
        await _svc.DeleteCustodialAsync(returnId, id, ct);
        return NoContent();
    }

    // ----------------------------------------------------------------- equity/debt interests
    [HttpGet("foreign-equity-debt")]
    [ProducesResponseType(typeof(IReadOnlyList<ForeignEquityDebtInterestDto>), StatusCodes.Status200OK)]
    public Task<IReadOnlyList<ForeignEquityDebtInterestDto>> ListEquityDebt([FromRoute] Guid returnId, CancellationToken ct)
        => _svc.ListEquityDebtAsync(returnId, ct);

    [HttpPost("foreign-equity-debt")]
    [ProducesResponseType(typeof(ForeignEquityDebtInterestDto), StatusCodes.Status200OK)]
    public Task<ForeignEquityDebtInterestDto> AddEquityDebt([FromRoute] Guid returnId, [FromBody] UpsertForeignEquityDebtInterestRequest request, CancellationToken ct)
        => _svc.AddEquityDebtAsync(returnId, request, ct);

    [HttpDelete("foreign-equity-debt/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteEquityDebt([FromRoute] Guid returnId, [FromRoute] Guid id, CancellationToken ct)
    {
        await _svc.DeleteEquityDebtAsync(returnId, id, ct);
        return NoContent();
    }

    // ----------------------------------------------------------------- immovable property (abroad)
    [HttpGet("foreign-immovable")]
    [ProducesResponseType(typeof(IReadOnlyList<ForeignImmovablePropertyFaDto>), StatusCodes.Status200OK)]
    public Task<IReadOnlyList<ForeignImmovablePropertyFaDto>> ListImmovable([FromRoute] Guid returnId, CancellationToken ct)
        => _svc.ListImmovableAsync(returnId, ct);

    [HttpPost("foreign-immovable")]
    [ProducesResponseType(typeof(ForeignImmovablePropertyFaDto), StatusCodes.Status200OK)]
    public Task<ForeignImmovablePropertyFaDto> AddImmovable([FromRoute] Guid returnId, [FromBody] UpsertForeignImmovablePropertyFaRequest request, CancellationToken ct)
        => _svc.AddImmovableAsync(returnId, request, ct);

    [HttpDelete("foreign-immovable/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteImmovable([FromRoute] Guid returnId, [FromRoute] Guid id, CancellationToken ct)
    {
        await _svc.DeleteImmovableAsync(returnId, id, ct);
        return NoContent();
    }

    // ----------------------------------------------------------------- financial interest in an entity
    [HttpGet("foreign-financial-interest")]
    [ProducesResponseType(typeof(IReadOnlyList<ForeignFinancialInterestDto>), StatusCodes.Status200OK)]
    public Task<IReadOnlyList<ForeignFinancialInterestDto>> ListFinancialInterest([FromRoute] Guid returnId, CancellationToken ct)
        => _svc.ListFinancialInterestAsync(returnId, ct);

    [HttpPost("foreign-financial-interest")]
    [ProducesResponseType(typeof(ForeignFinancialInterestDto), StatusCodes.Status200OK)]
    public Task<ForeignFinancialInterestDto> AddFinancialInterest([FromRoute] Guid returnId, [FromBody] UpsertForeignFinancialInterestRequest request, CancellationToken ct)
        => _svc.AddFinancialInterestAsync(returnId, request, ct);

    [HttpDelete("foreign-financial-interest/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteFinancialInterest([FromRoute] Guid returnId, [FromRoute] Guid id, CancellationToken ct)
    {
        await _svc.DeleteFinancialInterestAsync(returnId, id, ct);
        return NoContent();
    }
}
