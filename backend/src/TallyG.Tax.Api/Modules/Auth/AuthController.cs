using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TallyG.Tax.Api.Modules.Auth;

/// <summary>
/// Authentication endpoints. This controller is the canonical example for the codebase:
/// explicit api/v1 route, thin actions that delegate to the application service, DTO records
/// in/out, [AllowAnonymous] vs [Authorize], and errors surfaced via AppException (rendered as
/// RFC 7807 problem+json by the global middleware).
/// </summary>
[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _auth;

    public AuthController(IAuthService auth) => _auth = auth;

    /// <summary>Create a pending user and (optionally) provision the retail tenant.</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(RegisterResponse), StatusCodes.Status200OK)]
    public Task<RegisterResponse> Register([FromBody] RegisterRequest request, CancellationToken ct)
        => _auth.RegisterAsync(request, ct);

    /// <summary>Send a one-time passcode to a mobile/email for the given purpose.</summary>
    [HttpPost("otp/request")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(OtpRequestResponse), StatusCodes.Status200OK)]
    public Task<OtpRequestResponse> RequestOtp([FromBody] OtpRequestRequest request, CancellationToken ct)
        => _auth.RequestOtpAsync(request, ct);

    /// <summary>Verify an OTP and issue an access + refresh token pair.</summary>
    [HttpPost("otp/verify")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(OtpVerifyResponse), StatusCodes.Status200OK)]
    public Task<OtpVerifyResponse> VerifyOtp([FromBody] OtpVerifyRequest request, CancellationToken ct)
        => _auth.VerifyOtpAsync(request, GetClientIp(), ct);

    /// <summary>Rotate a refresh token and mint a new access token.</summary>
    [HttpPost("token/refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(RefreshResponse), StatusCodes.Status200OK)]
    public Task<RefreshResponse> Refresh([FromBody] RefreshRequest request, CancellationToken ct)
        => _auth.RefreshAsync(request, GetClientIp(), ct);

    /// <summary>Revoke the current session's refresh token. Idempotent.</summary>
    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest request, CancellationToken ct)
    {
        await _auth.LogoutAsync(request, ct);
        return NoContent();
    }

    /// <summary>Return the current authenticated principal.</summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(AuthUserDto), StatusCodes.Status200OK)]
    public Task<AuthUserDto> Me(CancellationToken ct) => _auth.GetMeAsync(ct);

    private string? GetClientIp() => HttpContext.Connection.RemoteIpAddress?.ToString();
}
