using TallyG.Tax.Domain.Common;
using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Domain.Entities;

/// <summary>A CRM sales lead (Ch.2 §2.9).</summary>
public class Lead : BaseEntity
{
    /// <summary>Null for system/global leads not yet attached to a tenant.</summary>
    public Guid? TenantId { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Mobile { get; set; }

    /// <summary>organic | referral | ad | partner.</summary>
    public string? Source { get; set; }

    public LeadStage Stage { get; set; } = LeadStage.New;
    public Guid? OwnerUserId { get; set; }
    public Guid? ConvertedUserId { get; set; }
    public int Score { get; set; }

    public ICollection<CrmActivity> Activities { get; set; } = new List<CrmActivity>();
}
