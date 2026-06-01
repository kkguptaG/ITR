using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TallyG.Tax.Domain.Abstractions;
using TallyG.Tax.Domain.Common;
using TallyG.Tax.Domain.Entities;
using TallyG.Tax.Domain.Enums;
using TallyG.Tax.Infrastructure.Persistence;

namespace TallyG.Tax.Api.Modules.Support;

/// <summary>
/// Notifications implementation. Owns the in-app inbox and the cross-module
/// <see cref="NotifyAsync"/> fan-out. Every notification is recorded as an in-app
/// <see cref="Notification"/> row (so it is visible in the dashboard bell) and, when the
/// requested channel is not in-app, also dispatched through <see cref="INotificationSender"/>
/// (the console/email/SMS stub). No manual DI registration — Scrutor binds this scoped.
/// </summary>
public sealed class NotificationService : INotificationService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly INotificationSender _sender;
    private readonly IDateTime _clock;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        AppDbContext db,
        ICurrentUser currentUser,
        INotificationSender sender,
        IDateTime clock,
        ILogger<NotificationService> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _sender = sender;
        _clock = clock;
        _logger = logger;
    }

    // ------------------------------------------------------------------- list

    public async Task<PagedResult<NotificationDto>> ListAsync(
        int page, int pageSize, bool unreadOnly, CancellationToken ct = default)
    {
        var (p, size) = SupportPaging.Normalize(page, pageSize);
        var userId = RequireUser();

        var query = _db.Notifications
            .AsNoTracking()
            .Where(n => n.TenantId == _currentUser.TenantId && n.UserId == userId);

        if (unreadOnly)
        {
            query = query.Where(n => n.ReadAt == null);
        }

        var total = await query.LongCountAsync(ct);

        // Order/page client-side: the Sqlite demo provider cannot ORDER BY DateTimeOffset, and
        // these lists are per-user (bounded). Postgres would translate it, but client evaluation
        // keeps one code path that is correct on both providers.
        var rows = await query.ToListAsync(ct);
        var items = rows
            .OrderByDescending(n => n.CreatedAt)
            .ThenByDescending(n => n.Id)
            .Skip((p - 1) * size)
            .Take(size)
            .Select(Map)
            .ToList();

        return new PagedResult<NotificationDto>(items, p, size, total);
    }

    // -------------------------------------------------------------- mark-read

    public async Task<MarkNotificationsReadResponse> MarkReadAsync(
        MarkNotificationsReadRequest request, CancellationToken ct = default)
    {
        var userId = RequireUser();
        var now = _clock.UtcNow;

        var query = _db.Notifications
            .Where(n => n.TenantId == _currentUser.TenantId && n.UserId == userId && n.ReadAt == null);

        // When specific ids are supplied, restrict to those; otherwise mark all unread.
        if (request.Ids is { Count: > 0 })
        {
            var ids = request.Ids.ToHashSet();
            query = query.Where(n => ids.Contains(n.Id));
        }

        var unread = await query.ToListAsync(ct);
        foreach (var n in unread)
        {
            n.ReadAt = now;
            if (n.Status != NotificationStatus.Failed)
            {
                n.Status = NotificationStatus.Read;
            }
        }

        if (unread.Count > 0)
        {
            await _db.SaveChangesAsync(ct);
        }

        return new MarkNotificationsReadResponse(unread.Count);
    }

    public async Task<int> UnreadCountAsync(CancellationToken ct = default)
    {
        var userId = RequireUser();
        return await _db.Notifications
            .Where(n => n.TenantId == _currentUser.TenantId && n.UserId == userId && n.ReadAt == null)
            .CountAsync(ct);
    }

    // ------------------------------------------------------- cross-module notify

    public async Task<Guid> NotifyAsync(
        Guid tenantId,
        Guid userId,
        string templateCode,
        string title,
        string body,
        NotificationChannel channel = NotificationChannel.InApp,
        IReadOnlyDictionary<string, string>? data = null,
        CancellationToken ct = default)
    {
        var now = _clock.UtcNow;

        var notification = new Notification
        {
            TenantId = tenantId,
            UserId = userId,
            Channel = channel,
            Template = templateCode,
            Title = title,
            Body = body,
            PayloadJson = data is { Count: > 0 } ? JsonSerializer.Serialize(data) : "{}",
            Status = NotificationStatus.Queued
        };

        _db.Notifications.Add(notification);

        // For an out-of-band channel, dispatch through the sender stub and record the outcome.
        if (channel != NotificationChannel.InApp)
        {
            var destination = await ResolveDestinationAsync(userId, channel, ct);
            try
            {
                await _sender.SendAsync(
                    new NotificationMessage(channel, destination, templateCode, title, body, data), ct);
                notification.Status = NotificationStatus.Sent;
                notification.SentAt = now;
            }
            catch (Exception ex)
            {
                // A failed external send must not roll back the in-app record.
                notification.Status = NotificationStatus.Failed;
                _logger.LogWarning(ex,
                    "Notification {Template} to user {UserId} on {Channel} failed to dispatch.",
                    templateCode, userId, channel);
            }
        }
        else
        {
            // In-app notifications are "delivered" the moment they are persisted.
            notification.Status = NotificationStatus.Sent;
            notification.SentAt = now;
        }

        await _db.SaveChangesAsync(ct);
        return notification.Id;
    }

    // ============================================================== internals

    private Guid RequireUser()
    {
        if (!_currentUser.IsAuthenticated)
        {
            throw new AppException("AUTH.UNAUTHENTICATED", "Not authenticated.", 401);
        }

        return _currentUser.UserId;
    }

    private async Task<string> ResolveDestinationAsync(Guid userId, NotificationChannel channel, CancellationToken ct)
    {
        var user = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.Email, u.MobileE164 })
            .FirstOrDefaultAsync(ct);

        return channel switch
        {
            NotificationChannel.Email => user?.Email ?? "unknown@local",
            NotificationChannel.Sms or NotificationChannel.WhatsApp => user?.MobileE164 ?? "+910000000000",
            _ => userId.ToString()
        };
    }

    private static NotificationDto Map(Notification n) => new(
        n.Id,
        n.Channel.ToString(),
        n.Template,
        n.Title,
        n.Body,
        n.Status.ToString(),
        n.ReadAt is not null,
        n.ReadAt,
        n.SentAt,
        n.CreatedAt);
}
