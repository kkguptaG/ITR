using TallyG.Tax.Domain.Common;

namespace TallyG.Tax.Domain.Entities;

/// <summary>Immutable, append-only trail of sensitive actions (Ch.2 §2.9).</summary>
public class AuditLog : BaseEntity
{
    /// <summary>Null for system events.</summary>
    public Guid? TenantId { get; set; }
    public Guid? ActorUserId { get; set; }

    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public Guid? EntityId { get; set; }

    /// <summary>Before/after/metadata payload (jsonb on Postgres, text on Sqlite).</summary>
    public string DataJson { get; set; } = "{}";

    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}
