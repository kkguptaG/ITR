using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using TallyG.Tax.Domain.Abstractions;
using TallyG.Tax.Domain.Common;
using TallyG.Tax.Domain.Entities;
using TallyG.Tax.Domain.Enums;
using TallyG.Tax.Infrastructure.Persistence;

namespace TallyG.Tax.Api.Modules.Auth;

/// <summary>
/// Canonical Auth implementation. This is the reference pattern other feature services copy:
/// constructor-injected dependencies, AppException for domain/validation failures, DTO records
/// in/out, and no manual DI registration (Scrutor auto-binds AuthService : IAuthService scoped).
/// </summary>
public sealed class AuthService : IAuthService
{
    private readonly AppDbContext _db;
    private readonly IPasswordlessTokenService _tokens;
    private readonly IOtpSender _otpSender;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTime _clock;
    private readonly IHostEnvironment _env;
    private readonly ILogger<AuthService> _logger;

    private readonly string _jwtIssuer;
    private readonly string _jwtAudience;
    private readonly SymmetricSecurityKey _signingKey;
    private readonly int _accessTokenMinutes;
    private readonly int _refreshTokenDays;
    private readonly int _otpTtlSeconds;
    private readonly bool _allowSelfRegistration;

    public AuthService(
        AppDbContext db,
        IPasswordlessTokenService tokens,
        IOtpSender otpSender,
        ICurrentUser currentUser,
        IDateTime clock,
        IHostEnvironment env,
        IConfiguration config,
        ILogger<AuthService> logger)
    {
        _db = db;
        _tokens = tokens;
        _otpSender = otpSender;
        _currentUser = currentUser;
        _clock = clock;
        _env = env;
        _logger = logger;

        var jwt = config.GetSection("Auth:Jwt");
        _jwtIssuer = jwt["Issuer"] ?? "tallyg.tax";
        _jwtAudience = jwt["Audience"] ?? "tallyg.tax.clients";
        var signingKeyValue = jwt["SigningKey"]
                              ?? "tallyg-dev-signing-key-please-override-in-config-0123456789";
        _signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKeyValue));
        _accessTokenMinutes = ParseInt(jwt["AccessTokenMinutes"], 15);
        _refreshTokenDays = ParseInt(jwt["RefreshTokenDays"], 30);
        _otpTtlSeconds = ParseInt(config["Auth:OtpTtlSeconds"], 300);
        // Lock-down switch: when "false", no new accounts can be created (neither the register
        // endpoint nor the signup-OTP path). Existing/seeded accounts can still sign in.
        _allowSelfRegistration = !string.Equals(
            config["Auth:AllowSelfRegistration"], "false", StringComparison.OrdinalIgnoreCase);
    }

    // ----------------------------------------------------------------- register

    public async Task<RegisterResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        if (!_allowSelfRegistration)
        {
            throw new AppException("AUTH.REGISTRATION_CLOSED",
                "Self-registration is disabled on this environment. Contact the administrator for access.", 403);
        }

        var email = Normalize(request.Email);
        var mobile = NormalizeMobile(request.Mobile);
        var fullName = request.FullName.Trim();

        if (fullName.Length == 0)
        {
            throw AppException.Validation("Full name is required.", "VALIDATION.NAME_REQUIRED");
        }

        if (email is null && mobile is null)
        {
            throw AppException.Validation("An email or mobile number is required.", "VALIDATION.CONTACT_REQUIRED");
        }

        // Self-serve B2C users live under the seeded retail tenant.
        var tenantId = await ResolveRetailTenantIdAsync(ct);

        // Idempotent on contact within the tenant: re-registering returns the existing user.
        var existing = await _db.Users.FirstOrDefaultAsync(
            u => u.TenantId == tenantId
                 && ((email != null && u.Email == email) || (mobile != null && u.MobileE164 == mobile)),
            ct);

        if (existing is not null)
        {
            return new RegisterResponse(existing.Id);
        }

        var user = new User
        {
            TenantId = tenantId,
            FullName = fullName,
            Email = email,
            MobileE164 = mobile,
            EmailVerified = false,
            MobileVerified = false,
            Status = UserStatus.Active
        };

        _db.Users.Add(user);

        // Every user gets the baseline "User" role.
        var userRole = await _db.Roles.FirstOrDefaultAsync(r => r.Name == "User", ct);
        if (userRole is not null)
        {
            _db.UserRoles.Add(new UserRole
            {
                UserId = user.Id,
                RoleId = userRole.Id,
                ScopeTenantId = Guid.Empty,
                GrantedAt = _clock.UtcNow
            });
        }

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Registered user {UserId} in tenant {TenantId}", user.Id, tenantId);

        return new RegisterResponse(user.Id);
    }

    // -------------------------------------------------------------- otp/request

    public async Task<OtpRequestResponse> RequestOtpAsync(OtpRequestRequest request, CancellationToken ct = default)
    {
        var purpose = ParsePurpose(request.Purpose);
        if (purpose == OtpPurpose.Signup && !_allowSelfRegistration)
        {
            throw new AppException("AUTH.REGISTRATION_CLOSED",
                "Self-registration is disabled on this environment.", 403);
        }

        var (identifier, channel) = ResolveIdentifier(request.Identifier);

        // Resolve a user if one exists (null for first-time signup OTPs).
        var tenantId = await ResolveRetailTenantIdAsync(ct);
        var user = channel == NotificationChannel.Email
            ? await _db.Users.FirstOrDefaultAsync(u => u.TenantId == tenantId && u.Email == identifier, ct)
            : await _db.Users.FirstOrDefaultAsync(u => u.TenantId == tenantId && u.MobileE164 == identifier, ct);

        // For a login OTP we require an existing account; signup OTPs may precede the user row.
        if (purpose == OtpPurpose.Login && user is null)
        {
            // Do not leak which identifiers are registered: behave as success but never deliver.
            // (A real system would also throttle; the rate-limit bucket lives in Program.cs / gateway.)
            _logger.LogInformation("Login OTP requested for unknown identifier; returning opaque token without delivery.");
        }

        var code = _tokens.GenerateCode();
        var now = _clock.UtcNow;

        var otp = new OtpToken
        {
            UserId = user?.Id,
            Identifier = identifier,
            Purpose = purpose,
            TokenHandle = _tokens.GenerateOpaqueToken(),
            CodeHash = _tokens.HashCode(code),
            Attempts = 0,
            MaxAttempts = 5,
            ExpiresAt = now.AddSeconds(_otpTtlSeconds)
        };

        _db.OtpTokens.Add(otp);
        await _db.SaveChangesAsync(ct);

        // STUB sender: logs to console and echoes the code back so dev flows can surface it.
        var sentCode = await _otpSender.SendAsync(identifier, channel, purpose, code, ct);

        // devOtp is exposed ONLY in Development (per the AUTH DTO CONTRACT).
        var devOtp = _env.IsDevelopment() ? sentCode : null;

        return new OtpRequestResponse(otp.TokenHandle, _otpTtlSeconds, devOtp);
    }

    // --------------------------------------------------------------- otp/verify

    public async Task<OtpVerifyResponse> VerifyOtpAsync(OtpVerifyRequest request, string? ip, CancellationToken ct = default)
    {
        var otp = await _db.OtpTokens.FirstOrDefaultAsync(o => o.TokenHandle == request.OtpToken, ct)
                  ?? throw new AppException("AUTH.OTP_INVALID", "Invalid or expired code.", 401);

        var now = _clock.UtcNow;

        if (otp.ConsumedAt is not null)
        {
            throw new AppException("AUTH.OTP_INVALID", "This code has already been used.", 401);
        }

        if (now >= otp.ExpiresAt)
        {
            throw new AppException("AUTH.OTP_INVALID", "Invalid or expired code.", 401);
        }

        if (otp.Attempts >= otp.MaxAttempts)
        {
            throw new AppException("AUTH.OTP_LOCKED", "Too many attempts. Request a new code.", 401);
        }

        if (!_tokens.VerifyCode(request.Code, otp.CodeHash))
        {
            otp.Attempts++;
            await _db.SaveChangesAsync(ct);
            throw new AppException("AUTH.OTP_INVALID", "Invalid or expired code.", 401);
        }

        // Code is correct — consume the challenge so it cannot be replayed.
        otp.ConsumedAt = now;

        // Resolve or create the user (signup OTP may precede the user row).
        var user = otp.UserId is { } uid
            ? await _db.Users.FirstOrDefaultAsync(u => u.Id == uid, ct)
            : await ResolveOrCreateUserForOtpAsync(otp, ct);

        if (user is null)
        {
            throw new AppException("AUTH.OTP_INVALID", "Invalid or expired code.", 401);
        }

        if (user.Status != UserStatus.Active)
        {
            throw new AppException("AUTH.ACCOUNT_DISABLED", "This account is not active.", 403);
        }

        // Mark the verified channel.
        if (IsEmail(otp.Identifier))
        {
            user.EmailVerified = true;
        }
        else
        {
            user.MobileVerified = true;
        }

        user.LastLoginAt = now;

        // Issue a fresh session: access JWT + opaque rotating refresh token.
        var sessionId = Guid.NewGuid();
        var roles = await LoadRolesAsync(user.Id, ct);
        var accessToken = MintAccessToken(user, roles, sessionId);
        var refreshToken = await IssueRefreshTokenAsync(user.Id, sessionId, ip, ct);

        await _db.SaveChangesAsync(ct);

        var dto = new AuthUserDto(user.Id, user.FullName, user.Email, user.MobileE164, roles);
        return new OtpVerifyResponse(accessToken, refreshToken, dto);
    }

    // ------------------------------------------------------------ token/refresh

    public async Task<RefreshResponse> RefreshAsync(RefreshRequest request, string? ip, CancellationToken ct = default)
    {
        var presentedHash = HashRefreshToken(request.RefreshToken);
        var token = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == presentedHash, ct)
                    ?? throw new AppException("AUTH.REFRESH_INVALID", "Invalid refresh token.", 401);

        var now = _clock.UtcNow;

        // Reuse detection: a token that was already rotated/revoked being presented again means
        // the token was stolen and replayed. Revoke the entire session family and force re-login.
        if (token.RevokedAt is not null || token.ReplacedById is not null)
        {
            await RevokeSessionFamilyAsync(token.UserId, token.SessionId, now, ct);
            await _db.SaveChangesAsync(ct);
            _logger.LogWarning(
                "Refresh-token reuse detected for user {UserId} session {SessionId}; family revoked.",
                token.UserId, token.SessionId);
            throw new AppException("AUTH.REFRESH_REUSE", "Refresh token reuse detected. Please sign in again.", 401);
        }

        if (now >= token.ExpiresAt)
        {
            throw new AppException("AUTH.REFRESH_INVALID", "Refresh token has expired.", 401);
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == token.UserId, ct);
        if (user is null || user.Status != UserStatus.Active)
        {
            throw new AppException("AUTH.REFRESH_INVALID", "Invalid refresh token.", 401);
        }

        // Rotate: revoke the presented token and issue a replacement on the same session.
        var newRefresh = await IssueRefreshTokenAsync(user.Id, token.SessionId, ip, ct);
        token.RevokedAt = now;

        var roles = await LoadRolesAsync(user.Id, ct);
        var accessToken = MintAccessToken(user, roles, token.SessionId);

        // Link the rotation chain (old -> new) for audit + reuse detection.
        var replacement = await _db.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == HashRefreshToken(newRefresh), ct);
        if (replacement is not null)
        {
            token.ReplacedById = replacement.Id;
        }

        await _db.SaveChangesAsync(ct);

        return new RefreshResponse(accessToken, newRefresh);
    }

    // ------------------------------------------------------------------ logout

    public async Task LogoutAsync(LogoutRequest request, CancellationToken ct = default)
    {
        var presentedHash = HashRefreshToken(request.RefreshToken);
        var token = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == presentedHash, ct);

        // Idempotent: logging out an already-invalid token is a no-op (204 either way).
        if (token is null || token.RevokedAt is not null)
        {
            return;
        }

        // Revoke the whole session (a session has one active refresh token at a time, but be safe).
        await RevokeSessionFamilyAsync(token.UserId, token.SessionId, _clock.UtcNow, ct);
        await _db.SaveChangesAsync(ct);
    }

    // ---------------------------------------------------------------------- me

    public async Task<AuthUserDto> GetMeAsync(CancellationToken ct = default)
    {
        if (!_currentUser.IsAuthenticated)
        {
            throw new AppException("AUTH.UNAUTHENTICATED", "Not authenticated.", 401);
        }

        var userId = _currentUser.UserId;
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct)
                   ?? throw AppException.NotFound("User not found.", "AUTH.USER_NOT_FOUND");

        var roles = await LoadRolesAsync(user.Id, ct);
        return new AuthUserDto(user.Id, user.FullName, user.Email, user.MobileE164, roles);
    }

    // ============================================================== internals

    private async Task<User?> ResolveOrCreateUserForOtpAsync(OtpToken otp, CancellationToken ct)
    {
        var tenantId = await ResolveRetailTenantIdAsync(ct);
        var isEmail = IsEmail(otp.Identifier);

        var user = isEmail
            ? await _db.Users.FirstOrDefaultAsync(u => u.TenantId == tenantId && u.Email == otp.Identifier, ct)
            : await _db.Users.FirstOrDefaultAsync(u => u.TenantId == tenantId && u.MobileE164 == otp.Identifier, ct);

        if (user is not null || otp.Purpose != OtpPurpose.Signup || !_allowSelfRegistration)
        {
            return user;
        }

        // Signup verification with no pre-created user row: provision one now.
        user = new User
        {
            TenantId = tenantId,
            FullName = isEmail ? otp.Identifier : "New User",
            Email = isEmail ? otp.Identifier : null,
            MobileE164 = isEmail ? null : otp.Identifier,
            Status = UserStatus.Active
        };
        _db.Users.Add(user);

        var userRole = await _db.Roles.FirstOrDefaultAsync(r => r.Name == "User", ct);
        if (userRole is not null)
        {
            _db.UserRoles.Add(new UserRole
            {
                UserId = user.Id,
                RoleId = userRole.Id,
                ScopeTenantId = Guid.Empty,
                GrantedAt = _clock.UtcNow
            });
        }

        return user;
    }

    private async Task<IReadOnlyList<string>> LoadRolesAsync(Guid userId, CancellationToken ct)
    {
        var roles = await _db.UserRoles
            .Where(ur => ur.UserId == userId)
            .Join(_db.Roles, ur => ur.RoleId, r => r.Id, (_, r) => r.Name)
            .Distinct()
            .ToListAsync(ct);

        return roles;
    }

    private string MintAccessToken(User user, IReadOnlyList<string> roles, Guid sessionId)
    {
        var now = _clock.UtcNow;
        var claims = new List<Claim>
        {
            new("sub", user.Id.ToString()),
            new("tid", user.TenantId.ToString()),
            new("sid", sessionId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim("role", role));
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _jwtIssuer,
            Audience = _jwtAudience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = now.AddMinutes(_accessTokenMinutes).UtcDateTime,
            Subject = new ClaimsIdentity(claims),
            SigningCredentials = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256)
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    private async Task<string> IssueRefreshTokenAsync(Guid userId, Guid sessionId, string? ip, CancellationToken ct)
    {
        // Opaque 256-bit random value (NOT a JWT) so it is revocable server-side.
        var raw = GenerateOpaqueRefreshValue();

        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = userId,
            SessionId = sessionId,
            TokenHash = HashRefreshToken(raw),
            ExpiresAt = _clock.UtcNow.AddDays(_refreshTokenDays),
            CreatedByIp = ip
        });

        await _db.SaveChangesAsync(ct);
        return raw;
    }

    private async Task RevokeSessionFamilyAsync(Guid userId, Guid sessionId, DateTimeOffset now, CancellationToken ct)
    {
        var family = await _db.RefreshTokens
            .Where(t => t.UserId == userId && t.SessionId == sessionId && t.RevokedAt == null)
            .ToListAsync(ct);

        foreach (var t in family)
        {
            t.RevokedAt = now;
        }
    }

    private async Task<Guid> ResolveRetailTenantIdAsync(CancellationToken ct)
    {
        // The seeded retail tenant is the home of self-serve B2C users.
        // (Slug is unique, so no ordering is needed — and SQLite cannot ORDER BY DateTimeOffset.)
        var tenantId = await _db.Tenants
            .Where(t => t.Slug == "retail")
            .Select(t => (Guid?)t.Id)
            .FirstOrDefaultAsync(ct);

        return tenantId
               ?? throw new AppException("AUTH.TENANT_MISSING", "Retail tenant is not provisioned.", 500);
    }

    private (string Identifier, NotificationChannel Channel) ResolveIdentifier(string raw)
    {
        if (IsEmail(raw))
        {
            return (Normalize(raw)!, NotificationChannel.Email);
        }

        var mobile = NormalizeMobile(raw)
                     ?? throw AppException.Validation("Provide a valid email or mobile number.", "VALIDATION.IDENTIFIER");
        return (mobile, NotificationChannel.Sms);
    }

    private static OtpPurpose ParsePurpose(string? purpose) => (purpose ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "login" => OtpPurpose.Login,
        "signup" or "register" => OtpPurpose.Signup,
        "reset" or "resetpassword" or "reset_password" => OtpPurpose.ResetPassword,
        "" => OtpPurpose.Login,
        _ => throw AppException.Validation($"Unsupported OTP purpose '{purpose}'.", "VALIDATION.OTP_PURPOSE")
    };

    private static bool IsEmail(string value) => value.Contains('@', StringComparison.Ordinal);

    private static string? Normalize(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed.ToLowerInvariant();
    }

    /// <summary>Best-effort E.164 normalization for the demo (India default country code).</summary>
    private static string? NormalizeMobile(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var digits = new string(value.Where(char.IsDigit).ToArray());
        if (digits.Length == 0)
        {
            return null;
        }

        if (value.TrimStart().StartsWith('+'))
        {
            return "+" + digits;
        }

        // 10-digit Indian mobile -> prefix +91; 12-digit starting 91 -> add '+'.
        return digits.Length switch
        {
            10 => "+91" + digits,
            12 when digits.StartsWith("91", StringComparison.Ordinal) => "+" + digits,
            _ => "+" + digits
        };
    }

    private static string GenerateOpaqueRefreshValue()
    {
        Span<byte> buffer = stackalloc byte[32];
        RandomNumberGenerator.Fill(buffer);
        return Convert.ToHexString(buffer);
    }

    private static string HashRefreshToken(string raw)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));

    private static int ParseInt(string? value, int fallback)
        => int.TryParse(value, out var parsed) ? parsed : fallback;
}
