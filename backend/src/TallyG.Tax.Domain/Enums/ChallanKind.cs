namespace TallyG.Tax.Domain.Enums;

/// <summary>Kind of self-paid tax challan (Schedule IT): advance tax (s.208/211) or
/// self-assessment tax (s.140A).</summary>
public enum ChallanKind
{
    Advance = 0,
    SelfAssessment = 1
}
