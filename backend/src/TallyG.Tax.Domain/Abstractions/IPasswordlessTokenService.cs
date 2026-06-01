namespace TallyG.Tax.Domain.Abstractions;

/// <summary>
/// Generates and verifies one-time passcodes for OTP-based (passwordless) auth.
/// Codes are never stored in plaintext — only a salted HMAC hash is persisted on the
/// OtpToken row, and verification is constant-time.
/// </summary>
public interface IPasswordlessTokenService
{
    /// <summary>Generate a fresh numeric OTP of the given length (default 6).</summary>
    string GenerateCode(int length = 6);

    /// <summary>Hash a code for at-rest storage (HMAC-SHA256, server-side key).</summary>
    string HashCode(string code);

    /// <summary>Constant-time comparison of a presented code against a stored hash.</summary>
    bool VerifyCode(string code, string codeHash);

    /// <summary>Opaque, unguessable handle that references a pending OTP challenge.</summary>
    string GenerateOpaqueToken();
}
