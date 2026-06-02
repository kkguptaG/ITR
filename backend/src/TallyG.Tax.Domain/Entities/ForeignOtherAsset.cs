using TallyG.Tax.Domain.Common;

namespace TallyG.Tax.Domain.Entities;

/// <summary>
/// Any other capital asset held outside India not covered by the specific Schedule FA tables, disclosed
/// in DetailsOthAssets. Same shape as foreign immovable / financial-interest (ownership + investment +
/// income offered). Return-scoped; non-disclosure carries Black Money Act penalties.
/// </summary>
public class ForeignOtherAsset : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid TaxReturnId { get; set; }

    public string CountryCode { get; set; } = string.Empty;
    public string CountryName { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;

    /// <summary>Nature of the asset (NatureOfAsset), e.g. "Art", "Intellectual property".</summary>
    public string NatureOfAsset { get; set; } = string.Empty;

    /// <summary>Ownership: DIRECT / BENEFICIAL_OWNER / BENIFICIARY (the ITD enum spelling).</summary>
    public string Ownership { get; set; } = "DIRECT";

    public DateOnly? AcquisitionDate { get; set; }

    public decimal TotalInvestment { get; set; }

    /// <summary>Income derived from the asset (IncDrvAsset).</summary>
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
