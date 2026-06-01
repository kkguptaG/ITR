using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using TallyG.Tax.Domain.Abstractions;

namespace TallyG.Tax.Infrastructure.Services;

/// <summary>
/// Generates numeric OTPs and stores only a salted HMAC-SHA256 of them. Verification is
/// constant-time. The HMAC key comes from configuration (Auth:OtpHashKey); a deterministic
/// dev fallback keeps the no-config demo working.
/// </summary>
public sealed class PasswordlessTokenService : IPasswordlessTokenService
{
    private readonly byte[] _key;

    public PasswordlessTokenService(IConfiguration configuration)
    {
        var configured = configuration["Auth:OtpHashKey"];
        _key = Encoding.UTF8.GetBytes(
            string.IsNullOrWhiteSpace(configured)
                ? "tallyg-dev-otp-hmac-key-change-me" // STUB: dev-only fallback secret
                : configured);
    }

    public string GenerateCode(int length = 6)
    {
        if (length is < 4 or > 10)
        {
            length = 6;
        }

        // Uniform numeric code without modulo bias.
        var max = (int)Math.Pow(10, length);
        var value = RandomNumberGenerator.GetInt32(0, max);
        return value.ToString().PadLeft(length, '0');
    }

    public string HashCode(string code)
    {
        using var hmac = new HMACSHA256(_key);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(code));
        return Convert.ToHexString(hash);
    }

    public bool VerifyCode(string code, string codeHash)
    {
        var computed = HashCode(code);
        // Constant-time comparison.
        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(computed),
            Encoding.ASCII.GetBytes(codeHash));
    }

    public string GenerateOpaqueToken()
    {
        Span<byte> buffer = stackalloc byte[32];
        RandomNumberGenerator.Fill(buffer);
        return Convert.ToHexString(buffer);
    }
}
