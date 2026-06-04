using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TallyG.Tax.Api.Modules.EVerify;

/// <summary>
/// Post-filing e-verification of a return. A filed ITR is only legally valid once verified within 30
/// days, via Aadhaar OTP / net-banking / bank-account / demat / bank-ATM EVC, or by posting a signed
/// ITR-V to CPC. Owner-scoped (the service enforces it). Non-CRUD actions use the ":verb" convention.
/// </summary>
[ApiController]
[Route("api/v1")]
[Authorize]
public sealed class EVerificationController : ControllerBase
{
    private readonly IEVerificationService _svc;

    public EVerificationController(IEVerificationService svc) => _svc = svc;

    /// <summary>Current e-verification state of the return + the 30-day window (reconciles a posted ITR-V).</summary>
    [HttpGet("returns/{id:guid}/e-verify")]
    [ProducesResponseType(typeof(EVerificationStatusDto), StatusCodes.Status200OK)]
    public Task<EVerificationStatusDto> Get([FromRoute] Guid id, CancellationToken ct)
        => _svc.GetAsync(id, ct);

    /// <summary>Begin verification: issue an OTP/EVC challenge for the chosen mode, or dispatch the ITR-V.</summary>
    [HttpPost("returns/{id:guid}/e-verify:start")]
    [ProducesResponseType(typeof(EVerificationStartResponse), StatusCodes.Status200OK)]
    public Task<EVerificationStartResponse> Start(
        [FromRoute] Guid id, [FromBody] EVerificationStartRequest request, CancellationToken ct)
        => _svc.StartAsync(id, request, ct);

    /// <summary>Complete an electronic verification by submitting the OTP/EVC (omit the code for net-banking).</summary>
    [HttpPost("returns/{id:guid}/e-verify:confirm")]
    [ProducesResponseType(typeof(EVerificationStatusDto), StatusCodes.Status200OK)]
    public Task<EVerificationStatusDto> Confirm(
        [FromRoute] Guid id, [FromBody] EVerificationConfirmRequest request, CancellationToken ct)
        => _svc.ConfirmAsync(id, request, ct);
}
