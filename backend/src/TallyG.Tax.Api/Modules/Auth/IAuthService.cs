namespace TallyG.Tax.Api.Modules.Auth;

/// <summary>
/// Application service backing the Auth module. Owns OTP issuance/verification, user
/// provisioning, JWT access-token minting, and rotating refresh tokens with reuse detection.
/// Auto-registered scoped by Scrutor (name pattern AuthService : IAuthService).
/// </summary>
public interface IAuthService
{
    /// <summary>Create a pending user (idempotent on email/mobile within the retail tenant).</summary>
    Task<RegisterResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default);

    /// <summary>Generate + "send" an OTP challenge; returns the opaque token (and devOtp in Development).</summary>
    Task<OtpRequestResponse> RequestOtpAsync(OtpRequestRequest request, CancellationToken ct = default);

    /// <summary>Verify an OTP challenge and, on success, issue an access + refresh token pair.</summary>
    Task<OtpVerifyResponse> VerifyOtpAsync(OtpVerifyRequest request, string? ip, CancellationToken ct = default);

    /// <summary>Rotate a refresh token, returning a new pair. Reuse of a rotated token revokes the session family.</summary>
    Task<RefreshResponse> RefreshAsync(RefreshRequest request, string? ip, CancellationToken ct = default);

    /// <summary>Revoke the refresh token (and thereby the session) for the current device.</summary>
    Task LogoutAsync(LogoutRequest request, CancellationToken ct = default);

    /// <summary>Return the current authenticated principal (from the access-token claims).</summary>
    Task<AuthUserDto> GetMeAsync(CancellationToken ct = default);
}
