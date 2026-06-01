using TallyG.Tax.Domain.Common;
using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Domain.Entities;

/// <summary>
/// Computed result, one row PER regime so old-vs-new is a two-row compare (Ch.2 §2.5).
/// The recommended regime is flagged. Engine details in Ch.3.
/// </summary>
public class TaxComputation : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid TaxReturnId { get; set; }
    public Guid? ReturnVersionId { get; set; }

    public Regime Regime { get; set; }

    public decimal GrossTotalIncome { get; set; }
    public decimal TotalDeductions { get; set; }

    /// <summary>Rounded to nearest ₹10 (s.288A).</summary>
    public decimal TaxableIncome { get; set; }

    public decimal TaxBeforeCess { get; set; }
    public decimal Cess { get; set; }
    public decimal Rebate87A { get; set; }
    public decimal Surcharge { get; set; }
    public decimal TotalTax { get; set; }

    public decimal TdsPaid { get; set; }
    public decimal AdvanceTax { get; set; }
    public decimal InterestPenalty { get; set; }

    /// <summary>Positive ⇒ refund, negative ⇒ payable.</summary>
    public decimal RefundOrPayable { get; set; }

    // AMT (s.115JC/JD) + reliefs (s.89/90/91); 0 when not applicable.
    public decimal AdjustedTotalIncome { get; set; }
    public decimal AlternativeMinimumTax { get; set; }
    public decimal AmtCreditGenerated { get; set; }
    public decimal AmtCreditSetOff { get; set; }
    public decimal Relief89 { get; set; }
    public decimal Relief90And91 { get; set; }

    public bool IsRecommended { get; set; }

    /// <summary>Line-by-line computation trace (jsonb on Postgres, text on Sqlite).</summary>
    public string TraceJson { get; set; } = "{}";

    public DateTimeOffset ComputedAt { get; set; } = DateTimeOffset.UtcNow;

    public TaxReturn? TaxReturn { get; set; }
}
