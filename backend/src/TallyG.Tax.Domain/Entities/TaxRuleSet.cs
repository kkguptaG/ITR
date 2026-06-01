using TallyG.Tax.Domain.Common;
using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Domain.Entities;

/// <summary>
/// The law-as-data store the tax engine reads (Ch.3). Slabs, caps, surcharge bands,
/// cess, 87A thresholds for BOTH regimes live in <see cref="RulesJson"/>, versioned by AY.
/// Append-only and immutable once active (superseding creates a new version).
/// </summary>
public class TaxRuleSet : BaseEntity
{
    public Guid AssessmentYearId { get; set; }

    /// <summary>Semver, e.g. "1.0.0".</summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>The complete rule-set document (jsonb on Postgres, text on Sqlite).</summary>
    public string RulesJson { get; set; } = "{}";

    public RuleSetStatus Status { get; set; } = RuleSetStatus.Draft;

    public DateOnly? EffectiveFrom { get; set; }
    public string? ContentHash { get; set; }

    public AssessmentYear? AssessmentYear { get; set; }
}
