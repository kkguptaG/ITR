using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TallyG.Tax.Api.Modules.ForeignAssets;

/// <summary>
/// Foreign bank/depository accounts for Schedule FA (resident disclosure). Owner-scoped in the service.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/returns/{returnId:guid}/foreign-bank-accounts")]
public sealed class ForeignAssetsController : ControllerBase
{
    private readonly IForeignBankAccountService _svc;

    public ForeignAssetsController(IForeignBankAccountService svc) => _svc = svc;

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ForeignBankAccountDto>), StatusCodes.Status200OK)]
    public Task<IReadOnlyList<ForeignBankAccountDto>> List([FromRoute] Guid returnId, CancellationToken ct)
        => _svc.ListAsync(returnId, ct);

    [HttpPost]
    [ProducesResponseType(typeof(ForeignBankAccountDto), StatusCodes.Status200OK)]
    public Task<ForeignBankAccountDto> Add([FromRoute] Guid returnId, [FromBody] UpsertForeignBankAccountRequest request, CancellationToken ct)
        => _svc.AddAsync(returnId, request, ct);

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete([FromRoute] Guid returnId, [FromRoute] Guid id, CancellationToken ct)
    {
        await _svc.DeleteAsync(returnId, id, ct);
        return NoContent();
    }
}
