using TallyG.Tax.Domain.Common;

namespace TallyG.Tax.Domain.Entities;

/// <summary>
/// Append-only, immutable snapshot of the full return payload at each meaningful
/// transition (computed, ca_approved, filed). Reproducibility core (Ch.2 §2.5).
/// </summary>
public class ReturnVersion : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid TaxReturnId { get; set; }

    public int VersionNo { get; set; }

    /// <summary>'computed' | 'ca_edit' | 'pre_file'.</summary>
    public string Reason { get; set; } = string.Empty;

    public string RuleSetVersion { get; set; } = string.Empty;

    /// <summary>Canonical ITR payload + computed breakdown (jsonb on Postgres, text on Sqlite).</summary>
    public string SnapshotJson { get; set; } = "{}";

    /// <summary>SHA-256 of the snapshot for tamper-evidence.</summary>
    public string JsonHash { get; set; } = string.Empty;

    public Guid? CreatedByUserId { get; set; }

    public TaxReturn? TaxReturn { get; set; }
}
