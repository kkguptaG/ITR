using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Api.Modules.Support;

/// <summary>
/// Application service for the Notifications module. Two responsibilities:
/// (1) the user-facing in-app inbox (list + mark-read), and
/// (2) a <see cref="NotifyAsync"/> entry point other feature modules (Payments, Filing,
/// CA-workflow) call to deliver a transactional notification — it persists an in-app
/// <c>Notification</c> row AND dispatches via the <c>INotificationSender</c> stub
/// (console/email). Auto-registered scoped by Scrutor (NotificationService : INotificationService).
/// </summary>
public interface INotificationService
{
    /// <summary>List the current user's in-app notifications, newest first (paged).</summary>
    Task<Domain.Common.PagedResult<NotificationDto>> ListAsync(
        int page, int pageSize, bool unreadOnly, CancellationToken ct = default);

    /// <summary>Mark the given notifications (or all unread when none specified) as read.</summary>
    Task<MarkNotificationsReadResponse> MarkReadAsync(
        MarkNotificationsReadRequest request, CancellationToken ct = default);

    /// <summary>Count of unread in-app notifications for the current user.</summary>
    Task<int> UnreadCountAsync(CancellationToken ct = default);

    /// <summary>
    /// Cross-module entry point: persist an in-app notification for <paramref name="userId"/>
    /// in <paramref name="tenantId"/> and dispatch it on <paramref name="channel"/> via the
    /// notification sender stub. Returns the created notification id. Safe to call from any
    /// module (it resolves the recipient's destination from the user record).
    /// </summary>
    Task<Guid> NotifyAsync(
        Guid tenantId,
        Guid userId,
        string templateCode,
        string title,
        string body,
        NotificationChannel channel = NotificationChannel.InApp,
        IReadOnlyDictionary<string, string>? data = null,
        CancellationToken ct = default);
}
