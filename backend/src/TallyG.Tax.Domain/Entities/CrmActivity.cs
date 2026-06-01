using TallyG.Tax.Domain.Common;

namespace TallyG.Tax.Domain.Entities;

/// <summary>A timeline activity on a CRM lead (Ch.2 §2.9).</summary>
public class CrmActivity : BaseEntity
{
    public Guid LeadId { get; set; }

    /// <summary>call | email | whatsapp | note | status_change.</summary>
    public string Type { get; set; } = "note";
    public string? Notes { get; set; }
    public Guid? PerformedByUserId { get; set; }

    public Lead? Lead { get; set; }
}
