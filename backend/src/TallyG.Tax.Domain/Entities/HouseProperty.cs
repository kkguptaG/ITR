using TallyG.Tax.Domain.Common;
using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Domain.Entities;

/// <summary>House-property head detail (Ch.2 §2.5).</summary>
public class HouseProperty : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid TaxReturnId { get; set; }

    public HousePropertyType Type { get; set; }
    public string? Address { get; set; }

    public decimal AnnualValue { get; set; }
    public decimal AnnualRent { get; set; }
    public decimal MunicipalTaxPaid { get; set; }
    public decimal StdDeduction30Pct { get; set; }
    public decimal InterestOnLoan { get; set; }
    public decimal CoOwnerSharePct { get; set; } = 100m;
    public decimal NetIncome { get; set; }

    public TaxReturn? TaxReturn { get; set; }
}
