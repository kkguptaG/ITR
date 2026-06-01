using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TallyG.Tax.Domain.Common;

namespace TallyG.Tax.Api.Modules.Support;

/// <summary>
/// In-app notification inbox for the current user (docs 04 §4.2 Notifications).
/// All actions are scoped to the authenticated principal; cross-user access is impossible
/// by construction (queries filter on the token's userId + tenantId).
/// </summary>
[ApiController]
[Route("api/v1/notifications")]
[Authorize]
public sealed class NotificationsController : ControllerBase
{
    private readonly INotificationService _notifications;

    public NotificationsController(INotificationService notifications) => _notifications = notifications;

    /// <summary>List the current user's in-app notifications (newest first, paged).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<NotificationDto>), StatusCodes.Status200OK)]
    public Task<PagedResult<NotificationDto>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = SupportPaging.DefaultPageSize,
        [FromQuery] bool unreadOnly = false,
        CancellationToken ct = default)
        => _notifications.ListAsync(page, pageSize, unreadOnly, ct);

    /// <summary>Count of unread notifications (drives the dashboard bell badge).</summary>
    [HttpGet("unread-count")]
    [ProducesResponseType(typeof(UnreadCountResponse), StatusCodes.Status200OK)]
    public async Task<UnreadCountResponse> UnreadCount(CancellationToken ct)
        => new(await _notifications.UnreadCountAsync(ct));

    /// <summary>
    /// Mark notifications read. With an explicit id list, only those are marked; with an
    /// empty/absent list, every unread notification for the user is marked read.
    /// </summary>
    [HttpPost(":mark-read")]
    [ProducesResponseType(typeof(MarkNotificationsReadResponse), StatusCodes.Status200OK)]
    public Task<MarkNotificationsReadResponse> MarkRead(
        [FromBody] MarkNotificationsReadRequest request, CancellationToken ct)
        => _notifications.MarkReadAsync(request, ct);
}

/// <summary>GET /notifications/unread-count response.</summary>
public sealed record UnreadCountResponse(int Unread);
