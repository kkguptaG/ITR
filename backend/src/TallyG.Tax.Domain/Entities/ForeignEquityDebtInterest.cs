using TallyG.Tax.Domain.Common;

namespace TallyG.Tax.Domain.Entities;

/// <summary>
/// A foreign equity or debt interest held by a resident, disclosed in Schedule FA
/// (DtlsForeignEquityDebtInterest) — e.g. shares of a foreign employer held directly (ESOP/RSU stock),
/// or foreign bonds. Return-scoped; non-disclosure carries Black Money Act penalties.
/// </summary>
public class ForeignEquityDebtInterest : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid TaxReturnId { get; set; }

    /// <summary>ITD numeric country code (excluding India), e.g. "2" = USA.</summary>
    public string CountryCode { get; set; } = string.Empty;
    public string CountryName { get; set; } = string.Empty;

    /// <summary>Name of the entity whose equity/debt is held (NameOfEntity).</summary>
    public string EntityName { get; set; } = string.Empty;
    public string EntityAddress { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;

    /// <summary>Nature of the entity / interest (e.g. "Equity", "Debt").</summary>
    public string NatureOfEntity { get; set; } = "Equity";

    /// <summary>Date the interest was acquired (InterestAcquiringDate).</summary>
    public DateOnly? AcquisitionDate { get; set; }

    /// <summary>Initial value of the investment (InitialValOfInvstmnt).</summary>
    public decimal InitialValue { get; set; }

    public decimal PeakBalance { get; set; }
    public decimal ClosingBalance { get; set; }

    /// <summary>Total gross amount paid / credited during the period (TotGrossAmtPaidCredited).</summary>
    public decimal GrossAmountCredited { get; set; }

    /// <summary>Total gross proceeds from sale / redemption (TotGrossProceeds).</summary>
    public decimal GrossProceeds { get; set; }

    public TaxReturn? TaxReturn { get; set; }
}
