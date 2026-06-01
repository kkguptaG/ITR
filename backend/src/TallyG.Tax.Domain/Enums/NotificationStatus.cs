namespace TallyG.Tax.Domain.Enums;

/// <summary>Delivery state of a notification.</summary>
public enum NotificationStatus
{
    Queued = 0,
    Sent = 1,
    Delivered = 2,
    Read = 3,
    Failed = 4
}
