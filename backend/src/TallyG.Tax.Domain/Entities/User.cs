using TallyG.Tax.Domain.Common;
using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Domain.Entities;

/// <summary>Authenticable identity. One human per (tenant, email/phone).</summary>
public class User : BaseEntity, ITenantScoped, ISoftDeletable
{
    public Guid TenantId { get; set; }

    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? MobileE164 { get; set; }

    public bool EmailVerified { get; set; }
    public bool MobileVerified { get; set; }

    /// <summary>Argon2id hash; null for OTP-only accounts.</summary>
    public string? PasswordHash { get; set; }

    // PAN: plaintext is never stored. Encrypted + masked + HMAC for lookup.
    public string? PanEnc { get; set; }
    public string? PanMasked { get; set; }
    public string? PanHash { get; set; }

    public UserStatus Status { get; set; } = UserStatus.Active;
    public DateTimeOffset? LastLoginAt { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    // Navigation
    public Tenant? Tenant { get; set; }
    public UserProfile? Profile { get; set; }
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<TaxReturn> TaxReturns { get; set; } = new List<TaxReturn>();
}
