using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TallyG.Tax.Api.Modules.Donations;

/// <summary>
/// Itemised 80G donations for the donee-wise Schedule 80G (ITR-2/3). Owner-scoped in the service.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/returns/{returnId:guid}/donations-80g")]
public sealed class DonationsController : ControllerBase
{
    private readonly IDonation80GService _svc;

    public DonationsController(IDonation80GService svc) => _svc = svc;

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<Donation80GDto>), StatusCodes.Status200OK)]
    public Task<IReadOnlyList<Donation80GDto>> List([FromRoute] Guid returnId, CancellationToken ct)
        => _svc.ListAsync(returnId, ct);

    [HttpPost]
    [ProducesResponseType(typeof(Donation80GDto), StatusCodes.Status200OK)]
    public Task<Donation80GDto> Add([FromRoute] Guid returnId, [FromBody] UpsertDonation80GRequest request, CancellationToken ct)
        => _svc.AddAsync(returnId, request, ct);

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete([FromRoute] Guid returnId, [FromRoute] Guid id, CancellationToken ct)
    {
        await _svc.DeleteAsync(returnId, id, ct);
        return NoContent();
    }
}
