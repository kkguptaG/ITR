using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TallyG.Tax.Api.Modules.BankAccounts;

/// <summary>
/// The assessee's bank accounts for filing. Add as many as needed; exactly one is the refund account
/// (set via :use-for-refund). All routes are owner/tenant-scoped in the service.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/bank-accounts")]
public sealed class BankAccountsController : ControllerBase
{
    private readonly IBankAccountService _svc;

    public BankAccountsController(IBankAccountService svc) => _svc = svc;

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<BankAccountDto>), StatusCodes.Status200OK)]
    public Task<IReadOnlyList<BankAccountDto>> List(CancellationToken ct) => _svc.ListAsync(ct);

    [HttpPost]
    [ProducesResponseType(typeof(BankAccountDto), StatusCodes.Status200OK)]
    public Task<BankAccountDto> Add([FromBody] UpsertBankAccountRequest request, CancellationToken ct)
        => _svc.AddAsync(request, ct);

    /// <summary>Mark this account as the one to credit any refund (clears the flag on the others).</summary>
    [HttpPost("{id:guid}:use-for-refund")]
    [ProducesResponseType(typeof(BankAccountDto), StatusCodes.Status200OK)]
    public Task<BankAccountDto> SetForRefund([FromRoute] Guid id, CancellationToken ct)
        => _svc.SetForRefundAsync(id, ct);

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete([FromRoute] Guid id, CancellationToken ct)
    {
        await _svc.DeleteAsync(id, ct);
        return NoContent();
    }
}
