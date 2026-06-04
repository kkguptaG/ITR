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

    /// <summary>The s.139 section this return is furnished under (original / belated / revised). Drives ReturnFileSec.</summary>
    public ReturnFilingSection FilingSection { get; set; } = ReturnFilingSection.Original;

    /// <summary>15-digit acknowledgment number of the original return (revised returns only → ITD ReceiptNo).</summary>
    public string? OriginalAcknowledgmentNumber { get; set; }

    /// <summary>Filing date of the original return (revised returns only → ITD OrigRetFiledDate).</summary>
    public DateOnly? OriginalFilingDate { get; set; }

    public string? AcknowledgmentNumber { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public DateTimeOffset? EVerifiedAt { get; set; }

    // --- Prepaid taxes (credits) + brought-forward losses captured on the return ---
    public decimal TdsPaid { get; set; }
    public decimal TcsPaid { get; set; }
    public decimal AdvanceTaxPaid { get; set; }
    public decimal SelfAssessmentTaxPaid { get; set; }
    public decimal BroughtForwardHousePropertyLoss { get; set; }
    public decimal BroughtForwardBusinessLoss { get; set; }
    public decimal BroughtForwardShortTermCapitalLoss { get; set; }
    public decimal BroughtForwardLongTermCapitalLoss { get; set; }

    // --- AMT credit (s.115JD) + reliefs (s.89/90/91) captured on the return ---
    /// <summary>Brought-forward AMT credit u/s 115JD (set off when regular tax exceeds AMT).</summary>
    public decimal BroughtForwardAmtCredit { get; set; }

    /// <summary>Relief u/s 89(1) for salary arrears (Form 10E), as computed by the assessee/CA.</summary>
    public decimal Relief89 { get; set; }

    /// <summary>Foreign income doubly taxed (India + abroad), for FTC u/s 90/90A/91.</summary>
    public decimal ForeignIncomeDoublyTaxed { get; set; }

    /// <summary>Foreign tax actually paid on that income (the credit ceiling).</summary>
    public decimal ForeignTaxPaid { get; set; }

    /// <summary>True ⇒ a DTAA exists with the source country (s.90/90A); false ⇒ unilateral relief u/s 91.</summary>
    public bool ForeignDtaaApplies { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    // Navigation
    public User? User { get; set; }
    public AssessmentYear? AssessmentYear { get; set; }
    public ICollection<ReturnVersion> Versions { get; set; } = new List<ReturnVersion>();
    public ICollection<IncomeSource> IncomeSources { get; set; } = new List<IncomeSource>();
    public ICollection<Deduction> Deductions { get; set; } = new List<Deduction>();
    public ICollection<TaxComputation> Computations { get; set; } = new List<TaxComputation>();
}
