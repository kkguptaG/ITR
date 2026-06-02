using TallyG.Tax.Domain.Common;

namespace TallyG.Tax.Domain.Entities;

/// <summary>
/// Income from any source outside India not already disclosed elsewhere, reported in Schedule FA
/// (DetailsOfOthSourcesIncOutsideIndia) — e.g. foreign consultancy income or a foreign pension.
/// Return-scoped; non-disclosure carries Black Money Act penalties.
/// </summary>
public class ForeignOtherIncome : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid TaxReturnId { get; set; }

    public string CountryCode { get; set; } = string.Empty;
    public string CountryName { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;

    /// <summary>Name of the person / entity paying the income (NameOfPerson).</summary>
    public string PayerName { get; set; } = string.Empty;
    public string PayerAddress { get; set; } = string.Empty;

    /// <summary>Income derived from the source (IncDerived).</summary>
    public decimal IncomeDerived { get; set; }

    /// <summary>Nature of the income (NatureOfInc).</summary>
    public string NatureOfIncome { get; set; } = string.Empty;

    /// <summary>Whether the income is taxable in the assessee's hands (IncDrvTaxFlag Y/N).</summary>
    public bool IncomeTaxable { get; set; }

    /// <summary>Income offered to tax in the return (IncOfferedAmt).</summary>
    public decimal IncomeOffered { get; set; }

    /// <summary>Which schedule the income was offered in: SA / HP / CG / OS / EI / NI.</summary>
    public string IncomeTaxSchedule { get; set; } = "OS";

    /// <summary>Item / row reference within that schedule.</summary>
    public string IncomeTaxScheduleItem { get; set; } = "1";

    public TaxReturn? TaxReturn { get; set; }
}
