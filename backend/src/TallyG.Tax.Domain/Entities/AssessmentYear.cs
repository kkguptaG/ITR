using TallyG.Tax.Domain.Common;

namespace TallyG.Tax.Domain.Entities;

/// <summary>
/// One immutable row per assessment year — the anchor for multi-year reproducibility
/// (Ch.2 §2.3). Returns pin their rule-set version from here at creation.
/// </summary>
public class AssessmentYear : BaseEntity
{
    /// <summary>e.g. "AY2025-26".</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>e.g. "FY2024-25".</summary>
    public string FyCode { get; set; } = string.Empty;

    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }

    public DateOnly DueDateNonAudit { get; set; }
    public DateOnly? DueDateAudit { get; set; }

    public bool IsActive { get; set; }
    public bool IsFilingOpen { get; set; } = true;

    /// <summary>Convention FK to the active <see cref="TaxRuleSet"/> version for this AY.</summary>
    public string RuleSetVersion { get; set; } = string.Empty;
}
