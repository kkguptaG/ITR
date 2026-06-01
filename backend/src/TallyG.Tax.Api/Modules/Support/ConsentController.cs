using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TallyG.Tax.Api.Modules.Support;

/// <summary>
/// DPDP consent endpoints (Ch.6 §6.2.1). Grants live under /consents; the user's own
/// receipts are read via /me/consents to mirror the "/me" self-resource convention.
/// All actions are scoped to the authenticated principal.
/// </summary>
[ApiController]
[Authorize]
public sealed class ConsentController : ControllerBase
{
    private readonly IConsentService _consents;

    public ConsentController(IConsentService consents) => _consents = consents;

    /// <summary>Record a purpose-bound, versioned consent for the current user.</summary>
    [HttpPost("api/v1/consents")]
    [ProducesResponseType(typeof(ConsentDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Grant([FromBody] GrantConsentRequest request, CancellationToken ct)
    {
        var dto = await _consents.GrantAsync(request, ct);
        // Explicit location (attribute-routed literal paths don't reliably resolve via CreatedAtAction).
        return Created($"/api/v1/consents/{dto.Id}", dto);
    }

    /// <summary>List the current user's consent receipts (active first).</summary>
    [HttpGet("api/v1/me/consents")]
    [ProducesResponseType(typeof(IReadOnlyList<ConsentDto>), StatusCodes.Status200OK)]
    public Task<IReadOnlyList<ConsentDto>> ListMine(CancellationToken ct) => _consents.ListMineAsync(ct);

    /// <summary>Withdraw (revoke) a consent the current user owns. Idempotent.</summary>
    [HttpDelete("api/v1/consents/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken ct)
    {
        await _consents.RevokeAsync(id, ct);
        return NoContent();
    }
}
