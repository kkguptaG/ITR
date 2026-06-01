using TallyG.Tax.Domain.Common;
using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Domain.Entities;

/// <summary>A queued/sent notification record (Ch.2 §2.8).</summary>
public class Notification : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }

    public NotificationChannel Channel { get; set; }
    public string Template { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? Body { get; set; }

    /// <summary>Template merge data (jsonb on Postgres, text on Sqlite).</summary>
    public string PayloadJson { get; set; } = "{}";

    public NotificationStatus Status { get; set; } = NotificationStatus.Queued;
    public DateTimeOffset? SentAt { get; set; }
    public DateTimeOffset? ReadAt { get; set; }
}
