using TallyG.Tax.Domain.Common;
using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Domain.Entities;

/// <summary>Short-lived, hashed OTP challenge (Ch.2 otp_tokens).</summary>
public class OtpToken : BaseEntity
{
    /// <summary>Null during signup, before a User row exists.</summary>
    public Guid? UserId { get; set; }

    /// <summary>The phone/email the code was sent to.</summary>
    public string Identifier { get; set; } = string.Empty;

    public OtpPurpose Purpose { get; set; }

    /// <summary>Opaque handle returned to the client; the client never sees the code hash.</summary>
    public string TokenHandle { get; set; } = string.Empty;

    /// <summary>HMAC of the 6-digit code; never plaintext.</summary>
    public string CodeHash { get; set; } = string.Empty;

    public int Attempts { get; set; }
    public int MaxAttempts { get; set; } = 5;

    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }

    public User? User { get; set; }
}
