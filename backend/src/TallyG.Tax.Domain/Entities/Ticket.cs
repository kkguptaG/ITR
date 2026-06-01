using TallyG.Tax.Domain.Common;
using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Domain.Entities;

/// <summary>A support ticket (Ch.2 §2.8).</summary>
public class Ticket : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid? TaxReturnId { get; set; }

    public string Subject { get; set; } = string.Empty;
    public string? Category { get; set; }
    public TicketStatus Status { get; set; } = TicketStatus.Open;
    public string Priority { get; set; } = "normal";

    public Guid? AssignedAgentId { get; set; }
    public DateTimeOffset? SlaDueAt { get; set; }

    public ICollection<TicketMessage> Messages { get; set; } = new List<TicketMessage>();
}
