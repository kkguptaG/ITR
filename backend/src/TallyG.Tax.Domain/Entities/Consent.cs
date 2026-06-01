using TallyG.Tax.Domain.Common;

namespace TallyG.Tax.Domain.Entities;

/// <summary>
/// DPDP consent receipt — versioned and immutable; each grant/withdrawal is a new row
/// (Ch.2 §2.9).
/// </summary>
public class Consent : BaseEntity
{
    public Guid UserId { get; set; }

    /// <summary>terms | privacy | dpdp_processing | ais_pull | ca_share | marketing.</summary>
    public string Purpose { get; set; } = string.Empty;
    public string Version { get; set; } = "1.0";

    public DateTimeOffset GrantedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? RevokedAt { get; set; }

    public string? IpAddress { get; set; }
}
