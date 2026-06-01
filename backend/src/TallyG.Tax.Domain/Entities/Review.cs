using TallyG.Tax.Domain.Common;
using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Domain.Entities;

/// <summary>A CA's review decision on an assignment (Ch.2 §2.8).</summary>
public class Review : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid CaAssignmentId { get; set; }

    public ReviewOutcome Outcome { get; set; }
    public string? Comments { get; set; }

    /// <summary>Optional review checklist (jsonb on Postgres, text on Sqlite).</summary>
    public string ChecklistJson { get; set; } = "{}";

    public short? RatingByCustomer { get; set; }

    public CaAssignment? CaAssignment { get; set; }
}
