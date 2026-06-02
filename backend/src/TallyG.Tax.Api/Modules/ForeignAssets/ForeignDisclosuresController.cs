using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TallyG.Tax.Api.Modules.ForeignAssets;

/// <summary>
/// Schedule FA signing-authority accounts + other-income-outside-India (ITR-2/3). Owner-scoped in the
/// service; two sibling resources declared per-method under the return.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/returns/{returnId:guid}")]
public sealed class ForeignDisclosuresController : ControllerBase
{
    private readonly IForeignDisclosuresService _svc;

    public ForeignDisclosuresController(IForeignDisclosuresService svc) => _svc = svc;

    // ----------------------------------------------------------------- signing authority
    [HttpGet("foreign-signing-authority")]
    [ProducesResponseType(typeof(IReadOnlyList<ForeignSigningAuthorityDto>), StatusCodes.Status200OK)]
    public Task<IReadOnlyList<ForeignSigningAuthorityDto>> ListSigningAuth([FromRoute] Guid returnId, CancellationToken ct)
        => _svc.ListSigningAuthAsync(returnId, ct);

    [HttpPost("foreign-signing-authority")]
    [ProducesResponseType(typeof(ForeignSigningAuthorityDto), StatusCodes.Status200OK)]
    public Task<ForeignSigningAuthorityDto> AddSigningAuth([FromRoute] Guid returnId, [FromBody] UpsertForeignSigningAuthorityRequest request, CancellationToken ct)
        => _svc.AddSigningAuthAsync(returnId, request, ct);

    [HttpDelete("foreign-signing-authority/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteSigningAuth([FromRoute] Guid returnId, [FromRoute] Guid id, CancellationToken ct)
    {
        await _svc.DeleteSigningAuthAsync(returnId, id, ct);
        return NoContent();
    }

    // ----------------------------------------------------------------- other income outside India
    [HttpGet("foreign-other-income")]
    [ProducesResponseType(typeof(IReadOnlyList<ForeignOtherIncomeDto>), StatusCodes.Status200OK)]
    public Task<IReadOnlyList<ForeignOtherIncomeDto>> ListOtherIncome([FromRoute] Guid returnId, CancellationToken ct)
        => _svc.ListOtherIncomeAsync(returnId, ct);

    [HttpPost("foreign-other-income")]
    [ProducesResponseType(typeof(ForeignOtherIncomeDto), StatusCodes.Status200OK)]
    public Task<ForeignOtherIncomeDto> AddOtherIncome([FromRoute] Guid returnId, [FromBody] UpsertForeignOtherIncomeRequest request, CancellationToken ct)
        => _svc.AddOtherIncomeAsync(returnId, request, ct);

    [HttpDelete("foreign-other-income/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteOtherIncome([FromRoute] Guid returnId, [FromRoute] Guid id, CancellationToken ct)
    {
        await _svc.DeleteOtherIncomeAsync(returnId, id, ct);
        return NoContent();
    }

    // ----------------------------------------------------------------- trusts outside India
    [HttpGet("foreign-trusts")]
    [ProducesResponseType(typeof(IReadOnlyList<ForeignTrustInterestDto>), StatusCodes.Status200OK)]
    public Task<IReadOnlyList<ForeignTrustInterestDto>> ListTrust([FromRoute] Guid returnId, CancellationToken ct)
        => _svc.ListTrustAsync(returnId, ct);

    [HttpPost("foreign-trusts")]
    [ProducesResponseType(typeof(ForeignTrustInterestDto), StatusCodes.Status200OK)]
    public Task<ForeignTrustInterestDto> AddTrust([FromRoute] Guid returnId, [FromBody] UpsertForeignTrustInterestRequest request, CancellationToken ct)
        => _svc.AddTrustAsync(returnId, request, ct);

    [HttpDelete("foreign-trusts/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteTrust([FromRoute] Guid returnId, [FromRoute] Guid id, CancellationToken ct)
    {
        await _svc.DeleteTrustAsync(returnId, id, ct);
        return NoContent();
    }
}
