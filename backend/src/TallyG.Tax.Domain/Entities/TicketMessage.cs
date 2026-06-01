using TallyG.Tax.Domain.Common;

namespace TallyG.Tax.Domain.Entities;

/// <summary>A message on a support ticket (Ch.2 §2.8).</summary>
public class TicketMessage : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid TicketId { get; set; }
    public Guid SenderUserId { get; set; }

    public string Body { get; set; } = string.Empty;

    /// <summary>customer | agent | system.</summary>
    public string SenderType { get; set; } = "customer";
    public bool IsInternalNote { get; set; }

    public Ticket? Ticket { get; set; }
}
