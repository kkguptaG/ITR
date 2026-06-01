using TallyG.Tax.Domain.Common;
using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Domain.Entities;

/// <summary>A referral from one user to another (Ch.2 §2.7).</summary>
public class Referral : BaseEntity
{
    public Guid ReferrerUserId { get; set; }
    public Guid? RefereeUserId { get; set; }

    public string Code { get; set; } = string.Empty;
    public ReferralStatus Status { get; set; } = ReferralStatus.Pending;

    public decimal RewardAmount { get; set; }
    public DateTimeOffset? QualifiedAt { get; set; }
}
