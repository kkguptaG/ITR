namespace TallyG.Tax.Domain.Enums;

/// <summary>Which TDS schedule a <see cref="Entities.TdsEntry"/> belongs to: salary (Form 16 /
/// Schedule TDS1) or anything else (Form 16A / Schedule TDS2).</summary>
public enum TdsHead
{
    Salary = 0,
    OtherThanSalary = 1
}
