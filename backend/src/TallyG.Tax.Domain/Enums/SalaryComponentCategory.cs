namespace TallyG.Tax.Domain.Enums;

/// <summary>
/// Schedule S salary-component classification — the "Salary Type" column of the
/// salary breakup grid. Drives how each line rolls up into the flat SalaryDetail fields.
/// </summary>
public enum SalaryComponentCategory
{
    /// <summary>Section 17(1) — basic, DA, bonus, grade pay, leave encashment in service, etc. (fully taxable).</summary>
    Salary = 0,

    /// <summary>Section 17(2) — perquisites (motor car, rent-free accommodation, ESOP, etc.).</summary>
    Perquisite = 1,

    /// <summary>Section 17(3) — profits in lieu of salary (severance, keyman, etc.).</summary>
    ProfitInLieu = 2,

    /// <summary>A section-10 allowance (HRA / LTC / conveyance / children education...); may be partly exempt.</summary>
    Allowance = 3,
}
