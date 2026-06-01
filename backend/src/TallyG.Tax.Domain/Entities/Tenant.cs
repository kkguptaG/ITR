using TallyG.Tax.Domain.Common;
using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Domain.Entities;

/// <summary>Top of the isolation hierarchy. B2C users sit under a system "retail" tenant.</summary>
public class Tenant : BaseEntity, ISoftDeletable
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public TenantType Type { get; set; } = TenantType.Retail;
    public string Status { get; set; } = "active";
    public string DataRegion { get; set; } = "in-central";

    /// <summary>Branding / feature-flag bag (jsonb on Postgres, text on Sqlite).</summary>
    public string SettingsJson { get; set; } = "{}";

    public DateTimeOffset? DeletedAt { get; set; }

    public ICollection<User> Users { get; set; } = new List<User>();
}
