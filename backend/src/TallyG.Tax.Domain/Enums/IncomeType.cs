namespace TallyG.Tax.Domain.Enums;

/// <summary>Income head for an <see cref="Entities.IncomeSource"/>.</summary>
public enum IncomeType
{
    Salary = 0,
    HouseProperty = 1,
    CapitalGains = 2,
    Business = 3,
    OtherSources = 4
}
