using TallyG.Tax.Domain.Common;
using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Domain.Entities;

/// <summary>
/// One deductor-wise TDS row from the assessee's 26AS / Form 16 / 16A — the building block of the
/// ITR's TDS schedules (TDSonSalaries / ScheduleTDS1 for salary; TDSonOthThanSals / ScheduleTDS2 for
/// the rest). Return-scoped (TDS credit belongs to a specific filing/AY).
/// </summary>
public class TdsEntry : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid TaxReturnId { get; set; }

    /// <summary>Salary (Form 16) vs everything else (Form 16A) — selects the schedule.</summary>
    public TdsHead Head { get; set; } = TdsHead.Salary;

    /// <summary>Deductor's TAN (e.g. DELH12345A).</summary>
    public string DeductorTan { get; set; } = string.Empty;

    public string DeductorName { get; set; } = string.Empty;

    /// <summary>The 26AS TDS section code for non-salary TDS (e.g. "94A" for 194A interest); null for salary.</summary>
    public string? TdsSection { get; set; }

    /// <summary>Income on which tax was deducted (Form 16 gross / 16A amount paid).</summary>
    public decimal IncomeOffered { get; set; }

    /// <summary>Tax actually deducted (and claimed this year).</summary>
    public decimal TaxDeducted { get; set; }

    public TaxReturn? TaxReturn { get; set; }
}
