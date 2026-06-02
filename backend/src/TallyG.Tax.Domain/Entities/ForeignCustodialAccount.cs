using TallyG.Tax.Domain.Common;

namespace TallyG.Tax.Domain.Entities;

/// <summary>
/// A foreign custodial / brokerage account held by a resident, disclosed in Schedule FA
/// (DtlsForeignCustodialAcc) — e.g. a Charles Schwab / Fidelity account holding vested RSUs. Return-scoped;
/// non-disclosure carries Black Money Act penalties.
/// </summary>
public class ForeignCustodialAccount : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid TaxReturnId { get; set; }

    /// <summary>ITD numeric country code (excluding India), e.g. "2" = USA.</summary>
    public string CountryCode { get; set; } = string.Empty;
    public string CountryName { get; set; } = string.Empty;

    /// <summary>Custodian / financial institution name (FinancialInstName).</summary>
    public string InstitutionName { get; set; } = string.Empty;
    public string InstitutionAddress { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;

    /// <summary>Account number (PII — encrypt at rest in production).</summary>
    public string AccountNumber { get; set; } = string.Empty;

    /// <summary>Ownership status: OWNER / BENEFICIAL_OWNER / BENIFICIARY (the ITD enum spelling).</summary>
    public string Status { get; set; } = "OWNER";

    public DateOnly? AccountOpenDate { get; set; }

    public decimal PeakBalance { get; set; }
    public decimal ClosingBalance { get; set; }

    /// <summary>Gross amount (interest / dividend / sale proceeds) paid or credited during the period.</summary>
    public decimal GrossAmountCredited { get; set; }

    /// <summary>Nature of the gross amount, coded: I=Interest, D=Dividend, S=Sale proceeds, O=Other, N=None.</summary>
    public string NatureOfAmount { get; set; } = "I";

    public TaxReturn? TaxReturn { get; set; }
}
