using TallyG.Tax.Domain.Common;

namespace TallyG.Tax.Domain.Entities;

/// <summary>
/// Immovable property held OUTSIDE India, disclosed in Schedule FA (DetailsImmovableProperty). Distinct
/// from <see cref="ImmovablePropertyAL"/> (the domestic Schedule AL asset). Return-scoped; non-disclosure
/// carries Black Money Act penalties.
/// </summary>
public class ForeignImmovablePropertyFA : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid TaxReturnId { get; set; }

    /// <summary>ITD numeric country code (excluding India).</summary>
    public string CountryCode { get; set; } = string.Empty;
    public string CountryName { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
    public string AddressOfProperty { get; set; } = string.Empty;

    /// <summary>Ownership: DIRECT / BENEFICIAL_OWNER / BENIFICIARY (the ITD enum spelling).</summary>
    public string Ownership { get; set; } = "DIRECT";

    public DateOnly? AcquisitionDate { get; set; }

    /// <summary>Total investment / cost of the property.</summary>
    public decimal TotalInvestment { get; set; }

    /// <summary>Income derived from the property during the year.</summary>
    public decimal IncomeDerived { get; set; }

    /// <summary>Nature of the income derived (free text).</summary>
    public string NatureOfIncome { get; set; } = string.Empty;

    /// <summary>The amount of that income offered to tax in the return.</summary>
    public decimal TaxableIncomeAmount { get; set; }

    /// <summary>Which schedule the income was offered in: SA / HP / CG / OS / EI / NI.</summary>
    public string IncomeTaxSchedule { get; set; } = "OS";

    /// <summary>Item / row reference within that schedule.</summary>
    public string IncomeTaxScheduleItem { get; set; } = "1";

    public TaxReturn? TaxReturn { get; set; }
}
