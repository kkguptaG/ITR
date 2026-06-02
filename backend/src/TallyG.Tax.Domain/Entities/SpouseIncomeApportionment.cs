using TallyG.Tax.Domain.Common;

namespace TallyG.Tax.Domain.Entities;

/// <summary>
/// Marks that the assessee is governed by the Portuguese Civil Code (Goa / Dadra &amp; Nagar Haveli /
/// Daman &amp; Diu), under which income — other than salary — is apportioned equally (50/50) between the
/// spouses. Disclosed in Schedule 5A. One record per return; holds the spouse's identity, while the
/// head-wise apportionment is derived from the return's own head incomes at the 50% statutory share.
/// </summary>
public class SpouseIncomeApportionment : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid TaxReturnId { get; set; }

    /// <summary>Name of the spouse the income is apportioned with (≤125 chars).</summary>
    public string SpouseName { get; set; } = string.Empty;

    /// <summary>PAN of the spouse (ITD format ABCDE1234F).</summary>
    public string SpousePan { get; set; } = string.Empty;

    /// <summary>Aadhaar of the spouse (optional; 12 digits).</summary>
    public string? SpouseAadhaar { get; set; }

    public TaxReturn? TaxReturn { get; set; }
}
