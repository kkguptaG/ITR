using TallyG.Tax.Domain.Common;
using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Domain.Entities;

/// <summary>Routes a return to a CA/reviewer; tracks the review SLA lifecycle (Ch.2 §2.8).</summary>
public class CaAssignment : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid TaxReturnId { get; set; }

    public Guid CaUserId { get; set; }
    public Guid AssignedByUserId { get; set; }

    public AssignmentStatus Status { get; set; } = AssignmentStatus.Unassigned;

    public string AssignmentType { get; set; } = "review";
    public short Priority { get; set; } = 5;
    public DateTimeOffset? SlaDueAt { get; set; }

    public DateTimeOffset AssignedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }

    public ICollection<Review> Reviews { get; set; } = new List<Review>();
}
