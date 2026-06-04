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

    // Per-section split of InterestPenalty (s.234A/B/C); feeds the ITR JSON IntrstPay node.
    public decimal Interest234A { get; set; }
    public decimal Interest234B { get; set; }
    public decimal Interest234C { get; set; }

    /// <summary>s.234F late-filing fee (flat, not interest); feeds the ITR JSON IntrstPay/Fee node.</summary>
    public decimal LateFee234F { get; set; }

    /// <summary>Positive ⇒ refund, negative ⇒ payable.</summary>
    public decimal RefundOrPayable { get; set; }

    // AMT (s.115JC/JD) + reliefs (s.89/90/91); 0 when not applicable.
    public decimal AdjustedTotalIncome { get; set; }
    public decimal AlternativeMinimumTax { get; set; }
    public decimal AmtCreditGenerated { get; set; }
    public decimal AmtCreditSetOff { get; set; }
    public decimal Relief89 { get; set; }
    public decimal Relief90And91 { get; set; }

    // Current-year losses carried forward after inter-head set-off (s.71); 0 when none. Feed next year's b/f.
    public decimal HousePropertyLossCarriedForward { get; set; }   // s.71B (8 years, vs HP income)
    public decimal BusinessLossCarriedForward { get; set; }        // s.72 (8 years, vs business income)
    public decimal SpeculativeLossCarriedForward { get; set; }     // s.73 (4 years, vs speculative income)
    public decimal ShortTermCapitalLossCarriedForward { get; set; } // s.74 (8 years, vs STCG/LTCG)
    public decimal LongTermCapitalLossCarriedForward { get; set; }  // s.74 (8 years, vs LTCG only)
    public decimal UnabsorbedDepreciationCarriedForward { get; set; } // s.32(2) (indefinite, vs any head exc. salary)

    public bool IsRecommended { get; set; }

    /// <summary>Line-by-line computation trace (jsonb on Postgres, text on Sqlite).</summary>
    public string TraceJson { get; set; } = "{}";

    public DateTimeOffset ComputedAt { get; set; } = DateTimeOffset.UtcNow;

    public TaxReturn? TaxReturn { get; set; }
}
