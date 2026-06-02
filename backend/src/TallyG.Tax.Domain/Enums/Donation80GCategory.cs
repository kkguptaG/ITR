namespace TallyG.Tax.Domain.Enums;

/// <summary>
/// Which Schedule 80G donation bucket a donee falls in. The four buckets map 1:1 to the official
/// ITR Schedule 80G tables and decide both the deduction rate (100% / 50%) and whether the donation
/// is subject to the 10%-of-adjusted-GTI qualifying limit ("ApprReqd").
/// </summary>
public enum Donation80GCategory
{
    /// <summary>100% deduction, no qualifying limit (Don100Percent) — e.g. PM CARES, National Defence Fund.</summary>
    HundredPercentNoLimit = 0,

    /// <summary>50% deduction, no qualifying limit (Don50PercentNoApprReqd) — e.g. PM Drought Relief Fund.</summary>
    FiftyPercentNoLimit = 1,

    /// <summary>100% deduction, subject to the 10%-of-GTI qualifying limit (Don100PercentApprReqd).</summary>
    HundredPercentWithLimit = 2,

    /// <summary>50% deduction, subject to the 10%-of-GTI qualifying limit (Don50PercentApprReqd) — most trusts.</summary>
    FiftyPercentWithLimit = 3
}
