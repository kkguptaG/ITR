using TallyG.Tax.Domain.Common;

namespace TallyG.Tax.Domain.Entities;

/// <summary>
/// Rotating refresh token with reuse detection (Ch.4). On refresh we revoke the old token
/// and link it to its replacement via <see cref="ReplacedById"/>.
/// </summary>
public class RefreshToken : BaseEntity
{
    public Guid UserId { get; set; }

    /// <summary>SHA-256 hash of the opaque token value (raw value never stored).</summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>Logical session id (JWT "sid") this token belongs to.</summary>
    public Guid SessionId { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }

    /// <summary>The token that superseded this one (rotation chain).</summary>
    public Guid? ReplacedById { get; set; }

    public string? CreatedByIp { get; set; }

    public bool IsActive => RevokedAt is null && DateTimeOffset.UtcNow < ExpiresAt;

    public User? User { get; set; }
}
