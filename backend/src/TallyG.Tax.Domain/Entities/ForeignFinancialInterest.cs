using TallyG.Tax.Domain.Common;

namespace TallyG.Tax.Domain.Entities;

/// <summary>
/// A financial interest in any entity held outside India, disclosed in Schedule FA
/// (DetailsFinancialInterest) — e.g. shares of / a partnership in a foreign company or LLC. Return-scoped;
/// non-disclosure carries Black Money Act penalties.
/// </summary>
public class ForeignFinancialInterest : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid TaxReturnId { get; set; }

    /// <summary>ITD numeric country code (excluding India).</summary>
    public string CountryCode { get; set; } = string.Empty;
    public string CountryName { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;

    /// <summary>Nature of the entity (optional, e.g. "Company", "Partnership").</summary>
    public string NatureOfEntity { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public string EntityAddress { get; set; } = string.Empty;

    /// <summary>Nature of interest: DIRECT / BENEFICIAL_OWNER / BENIFICIARY (the ITD enum spelling).</summary>
    public string NatureOfInterest { get; set; } = "DIRECT";

    public DateOnly? DateHeld { get; set; }

    public decimal TotalInvestment { get; set; }

    /// <summary>Income derived from the interest during the year.</summary>
    public decimal IncomeFromInterest { get; set; }

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
