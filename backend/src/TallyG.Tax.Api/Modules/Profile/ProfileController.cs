using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TallyG.Tax.Api.Modules.Profile;

/// <summary>
/// The signed-in user's KYC / assessee profile. Used by the post-login onboarding and Settings.
/// Owner-scoped (the service resolves the current user); errors render as RFC 7807 problem+json.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/profile")]
public sealed class ProfileController : ControllerBase
{
    private readonly IProfileService _svc;

    public ProfileController(IProfileService svc) => _svc = svc;

    /// <summary>The current user's KYC profile + an <c>isComplete</c> flag (gates onboarding).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ProfileDto), StatusCodes.Status200OK)]
    public Task<ProfileDto> Get(CancellationToken ct) => _svc.GetAsync(ct);

    /// <summary>Upsert the KYC profile (PAN is masked + hashed; the rest lands on the user profile).</summary>
    [HttpPut]
    [ProducesResponseType(typeof(ProfileDto), StatusCodes.Status200OK)]
    public Task<ProfileDto> Update([FromBody] UpdateProfileRequest request, CancellationToken ct)
        => _svc.UpdateAsync(request, ct);
}
