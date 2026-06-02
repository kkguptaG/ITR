using TallyG.Tax.Domain.Common;
using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Domain.Entities;

/// <summary>
/// One exempt-income item disclosed in Schedule EI (ITR-2/3). Exempt income is not chargeable to tax
/// (so it never enters the engine's GTI) but the ITD still requires it to be reported — and net
/// agricultural income above ₹5,000 is used for the rate (partial integration). Return-scoped.
/// </summary>
public class ExemptIncome : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid TaxReturnId { get; set; }

    /// <summary>Which Schedule EI line this item lands on.</summary>
    public ExemptIncomeCategory Category { get; set; }

    /// <summary>Free-text nature of the income (≤125 chars; emitted as OthNatOfInc for "Other" rows).</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>The exempt amount (whole rupees on the return).</summary>
    public decimal Amount { get; set; }

    // --- Agricultural land details (only for Category=Agricultural; required by the schedule's
    //     ExcNetAgriIncDtls table when net agricultural income exceeds ₹5,00,000). All optional. ---

    /// <summary>District where the agricultural land is located.</summary>
    public string? District { get; set; }

    /// <summary>6-digit PIN code of the agricultural land.</summary>
    public string? PinCode { get; set; }

    /// <summary>Measurement of the land (in acres; the schedule allows 2 decimal places).</summary>
    public decimal? LandMeasurement { get; set; }

    /// <summary>True = owned (O); false = held on lease (H).</summary>
    public bool? LandOwned { get; set; }

    /// <summary>True = irrigated (IRG); false = rain-fed (RF).</summary>
    public bool? LandIrrigated { get; set; }

    public TaxReturn? TaxReturn { get; set; }
}
