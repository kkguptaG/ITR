namespace TallyG.Tax.Domain.Enums;

/// <summary>
/// The head under which a specified person's income is clubbed into the assessee's income (s.64),
/// mapping to Schedule SPI's HeadIncIncluded enum (SA/HP/CG/OS/EI, plus BP on ITR-3).
/// </summary>
public enum ClubbedIncomeHead
{
    Salary = 0,
    HouseProperty = 1,
    CapitalGains = 2,
    OtherSources = 3,
    ExemptIncome = 4,

    /// <summary>Business/profession — an ITR-3-only SPI head (skipped when the form is ITR-2).</summary>
    Business = 5
}
