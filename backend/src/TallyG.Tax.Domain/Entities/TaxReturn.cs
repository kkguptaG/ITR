using TallyG.Tax.Domain.Common;
using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Domain.Entities;

/// <summary>
/// Aggregate root of a filing — one row per (user, AY, attempt). The mutable "working"
/// header; immutable snapshots live in <see cref="ReturnVersion"/> (Ch.2 §2.5).
/// </summary>
public class TaxReturn : BaseEntity, ITenantScoped, ISoftDeletable
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid AssessmentYearId { get; set; }

    /// <summary>Null until the auto-selector classifies the return.</summary>
    public ItrType? ItrType { get; set; }

    public ReturnStatus Status { get; set; } = ReturnStatus.Draft;

    /// <summary>Null until chosen/auto-selected.</summary>
    public Regime? Regime { get; set; }

    /// <summary>Rule-set + questionnaire versions frozen at creation (pin-on-file, Ch.3 §3.11).</summary>
    public string RuleSetVersion { get; set; } = string.Empty;
    public string QuestionnaireSchemaVersion { get; set; } = string.Empty;

    /// <summary>Flexible interview answers (jsonb on Postgres, text on Sqlite).</summary>
    public string AnswersJson { get; set; } = "{}";

    public string FilingMode { get; set; } = "self";

    public bool IsRevised { get; set; }
    public Guid? OriginalReturnId { get; set; }

    public string? AcknowledgmentNumber { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public DateTimeOffset? EVerifiedAt { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    // Navigation
    public User? User { get; set; }
    public AssessmentYear? AssessmentYear { get; set; }
    public ICollection<ReturnVersion> Versions { get; set; } = new List<ReturnVersion>();
    public ICollection<IncomeSource> IncomeSources { get; set; } = new List<IncomeSource>();
    public ICollection<Deduction> Deductions { get; set; } = new List<Deduction>();
    public ICollection<TaxComputation> Computations { get; set; } = new List<TaxComputation>();
}
