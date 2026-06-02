namespace TallyG.Tax.Domain.Enums;

/// <summary>
/// The section under which double-taxation relief on foreign-source income is claimed, reported in
/// Schedule TR1's ReliefClaimedUsSection. s.90 / s.90A are treaty (DTAA) relief; s.91 is unilateral.
/// </summary>
public enum ForeignTaxReliefSection
{
    /// <summary>s.90 — relief under a DTAA with a notified country.</summary>
    Section90 = 0,

    /// <summary>s.90A — relief under an agreement adopted by a specified association.</summary>
    Section90A = 1,

    /// <summary>s.91 — unilateral relief where no DTAA exists.</summary>
    Section91 = 2
}
