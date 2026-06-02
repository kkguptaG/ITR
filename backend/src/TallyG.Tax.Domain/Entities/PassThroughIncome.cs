using TallyG.Tax.Domain.Common;
using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Domain.Entities;

/// <summary>
/// One pass-through income component received from a business trust (s.115UA), investment fund
/// (s.115UB) or securitisation trust (s.115U) and disclosed in Schedule PTI. The income retains its
/// character (house property, capital gains by rate bucket, dividend, other sources) in the unitholder's
/// hands. Captured per (investment, category) so one investment can pass through several heads.
/// Return-scoped.
/// </summary>
public class PassThroughIncome : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid TaxReturnId { get; set; }

    /// <summary>Name of the pass-through entity (≤125 chars).</summary>
    public string BusinessName { get; set; } = string.Empty;

    /// <summary>PAN of the pass-through entity (ITD format ABCDE1234F).</summary>
    public string BusinessPan { get; set; } = string.Empty;

    /// <summary>Which section the investment is covered under (115UA / 115UB / 115U).</summary>
    public PassThroughInvestmentType InvestmentType { get; set; }

    /// <summary>The head / rate bucket this income component falls in.</summary>
    public PassThroughIncomeCategory Category { get; set; }

    /// <summary>Amount of income passed through under this category.</summary>
    public decimal AmountOfIncome { get; set; }

    /// <summary>The unitholder's share of the fund's current-year loss (HP / capital-gains buckets only).</summary>
    public decimal CurrentYearLossShare { get; set; }

    /// <summary>TDS deducted by the pass-through entity on this component.</summary>
    public decimal TdsAmount { get; set; }

    public TaxReturn? TaxReturn { get; set; }
}
