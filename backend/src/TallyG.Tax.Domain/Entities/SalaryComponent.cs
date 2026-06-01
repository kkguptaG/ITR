using TallyG.Tax.Domain.Common;
using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Domain.Entities;

/// <summary>
/// One line of a Schedule S salary breakup (a row in the "Breakup Salary" grid): a named
/// component classified as 17(1) salary / 17(2) perquisite / 17(3) profit-in-lieu / a s.10
/// allowance, carrying its gross amount and the portion exempt under the Act.
/// Rolls up into the flat <see cref="SalaryDetail"/> fields the tax engine consumes (see SalaryRollup).
/// </summary>
public class SalaryComponent : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid SalaryDetailId { get; set; }

    /// <summary>Display name of the component, e.g. "Basic Salary", "House Rent Allowance".</summary>
    public string Label { get; set; } = string.Empty;

    public SalaryComponentCategory Category { get; set; } = SalaryComponentCategory.Salary;

    /// <summary>Gross amount of this component.</summary>
    public decimal Total { get; set; }

    /// <summary>Portion of <see cref="Total"/> exempt under the Act (e.g. s.10 allowance exemption). 0..Total.</summary>
    public decimal Exempt { get; set; }

    /// <summary>True for the House Rent Allowance row: its exempt part feeds the s.10(13A) HRA exemption
    /// (which the engine gates to the OLD regime), keeping it distinct from other s.10 allowances.</summary>
    public bool IsHra { get; set; }

    public SalaryDetail? SalaryDetail { get; set; }
}
