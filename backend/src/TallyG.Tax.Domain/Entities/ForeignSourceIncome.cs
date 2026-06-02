using TallyG.Tax.Domain.Common;
using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Domain.Entities;

/// <summary>
/// One foreign-source income line — income earned outside India, taxed there, on which double-taxation
/// relief (s.90/90A/91) is claimed in India. Disclosed in Schedule FSI (country × head) and Schedule TR1
/// (country-wise relief). Captured per (country, head) so a country can have income under several heads.
/// Return-scoped. (Relevant only to a resident, who is taxed on global income.)
/// </summary>
public class ForeignSourceIncome : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid TaxReturnId { get; set; }

    /// <summary>ITD country code (CountryCodeExcludingIndia enum; e.g. "1" = USA, "44" = UK).</summary>
    public string CountryCode { get; set; } = string.Empty;

    /// <summary>Country name (≤55 chars).</summary>
    public string CountryName { get; set; } = string.Empty;

    /// <summary>Taxpayer Identification Number in that country (≤75 chars).</summary>
    public string TaxIdentificationNo { get; set; } = string.Empty;

    /// <summary>Which head of income this foreign income is offered under.</summary>
    public ForeignIncomeHead Head { get; set; }

    /// <summary>Income from outside India under this head (whole rupees).</summary>
    public decimal IncomeFromOutsideIndia { get; set; }

    /// <summary>Tax paid outside India on that income (whole rupees).</summary>
    public decimal TaxPaidOutsideIndia { get; set; }

    /// <summary>Section under which the double-taxation relief is claimed (90 / 90A / 91).</summary>
    public ForeignTaxReliefSection ReliefSection { get; set; }

    /// <summary>The DTAA article under which relief is claimed (≤16 chars; e.g. "Article 23"). Optional.</summary>
    public string? DtaaArticle { get; set; }

    public TaxReturn? TaxReturn { get; set; }
}
