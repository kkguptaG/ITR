using TallyG.Tax.Domain.Common;
using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Domain.Entities;

/// <summary>
/// Income of a specified person (spouse, minor child, …) clubbed into the assessee's income under s.64,
/// disclosed in Schedule SPI. The clubbed amount is already included in the relevant head's total; this
/// schedule just attributes it to the specified person. Return-scoped.
/// </summary>
public class ClubbedIncome : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid TaxReturnId { get; set; }

    /// <summary>Name of the specified person whose income is clubbed (≤125 chars).</summary>
    public string SpecifiedPersonName { get; set; } = string.Empty;

    /// <summary>PAN of the specified person (optional; ITD format ABCDE1234F).</summary>
    public string? Pan { get; set; }

    /// <summary>Aadhaar of the specified person (optional; 12 digits).</summary>
    public string? Aadhaar { get; set; }

    /// <summary>Relationship to the assessee (e.g. "Minor son", "Spouse"; ≤50 chars).</summary>
    public string Relationship { get; set; } = string.Empty;

    /// <summary>Amount of the specified person's income clubbed into the assessee's income.</summary>
    public decimal AmountIncluded { get; set; }

    /// <summary>The head under which the clubbed amount is included.</summary>
    public ClubbedIncomeHead IncomeHead { get; set; }

    public TaxReturn? TaxReturn { get; set; }
}
