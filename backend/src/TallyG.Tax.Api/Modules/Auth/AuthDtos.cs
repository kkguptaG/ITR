// Auth module — request/response DTOs.
// These records ARE the public auth contract consumed verbatim by the frontend
// (see docs/architecture/04-api-and-auth.md and the project AUTH DTO CONTRACT).
// JSON is camelCase on the wire (ASP.NET Core default), mapping to these PascalCase records.

namespace TallyG.Tax.Api.Modules.Auth;

// --- register ---

/// <summary>POST /auth/register body.</summary>
public sealed record RegisterRequest(string FullName, string Email, string Mobile);

/// <summary>POST /auth/register response: the id of the newly created (pending) user.</summary>
public sealed record RegisterResponse(Guid UserId);

// --- otp/request ---

/// <summary>
/// POST /auth/otp/request body. <paramref name="Identifier"/> is an email or an
/// E.164 mobile number; <paramref name="Purpose"/> is one of: login, signup, reset.
/// </summary>
public sealed record OtpRequestRequest(string Identifier, string Purpose);

/// <summary>
/// POST /auth/otp/request response. <see cref="DevOtp"/> is populated ONLY in the
/// Development environment so the demo can log in without a real SMS provider.
/// </summary>
public sealed record OtpRequestResponse(string OtpToken, int ExpiresInSeconds, string? DevOtp);

// --- otp/verify ---

/// <summary>POST /auth/otp/verify body.</summary>
public sealed record OtpVerifyRequest(string OtpToken, string Code);

/// <summary>The authenticated principal echoed back to the client on login / from /auth/me.</summary>
public sealed record AuthUserDto(
    Guid Id,
    string FullName,
    string? Email,
    string? Mobile,
    IReadOnlyList<string> Roles);

/// <summary>POST /auth/otp/verify response: the freshly issued token pair + the user.</summary>
public sealed record OtpVerifyResponse(string AccessToken, string RefreshToken, AuthUserDto User);

// --- token/refresh ---

/// <summary>POST /auth/token/refresh body.</summary>
public sealed record RefreshRequest(string RefreshToken);

/// <summary>POST /auth/token/refresh response: a rotated token pair.</summary>
public sealed record RefreshResponse(string AccessToken, string RefreshToken);

// --- logout ---

/// <summary>POST /auth/logout body.</summary>
public sealed record LogoutRequest(string RefreshToken);
