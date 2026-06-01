using TallyG.Tax.Domain.Common;

namespace TallyG.Tax.Domain.Entities;

/// <summary>Chartered-accountant profile attached to a user (Ch.2 §2.8).</summary>
public class CaProfile : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }

    public string FirmName { get; set; } = string.Empty;

    /// <summary>ICAI membership number.</summary>
    public string? Membership { get; set; }

    public decimal Rating { get; set; }
    public int TotalReviews { get; set; }
    public int MaxConcurrentReturns { get; set; } = 25;

    public bool Active { get; set; } = true;
    public bool IsVerified { get; set; }

    public ICollection<CaAssignment> Assignments { get; set; } = new List<CaAssignment>();
}
