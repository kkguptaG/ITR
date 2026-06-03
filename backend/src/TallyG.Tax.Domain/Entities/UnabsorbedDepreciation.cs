using TallyG.Tax.Domain.Common;

namespace TallyG.Tax.Domain.Entities;

/// <summary>
/// Brought-forward unabsorbed depreciation / allowance from a prior assessment year (s.32(2)), disclosed
/// in Schedule UD. Unabsorbed depreciation carries forward indefinitely and sets off against any head of
/// income in a later year. Captured per prior AY. Return-scoped (ITR-3).
/// </summary>
public class UnabsorbedDepreciation : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid TaxReturnId { get; set; }

    /// <summary>The assessment year the loss relates to, "YYYY-YY" (e.g. "2023-24").</summary>
    public string AssessmentYearLabel { get; set; } = string.Empty;

    /// <summary>Brought-forward unabsorbed depreciation from that AY.</summary>
    public decimal UnabsorbedDepreciationAmount { get; set; }

    /// <summary>Brought-forward unabsorbed allowance (other than depreciation, e.g. s.35(4)) from that AY.</summary>
    public decimal UnabsorbedAllowanceAmount { get; set; }

    public TaxReturn? TaxReturn { get; set; }
}
