namespace TallyG.Tax.Domain.Enums;

/// <summary>
/// The head of income a foreign-source income item is offered under, mapping to the per-head columns
/// of Schedule FSI (IncFromSal / IncFromHP / IncCapGain / IncOthSrc, plus IncFromBusiness on ITR-3).
/// </summary>
public enum ForeignIncomeHead
{
    Salary = 0,
    HouseProperty = 1,
    CapitalGains = 2,
    OtherSources = 3,

    /// <summary>Business/profession — an ITR-3-only FSI column (ignored when the form is ITR-2).</summary>
    Business = 4
}
