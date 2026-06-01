using Microsoft.EntityFrameworkCore;
using TallyG.Tax.Api.Modules.Support;
using TallyG.Tax.Domain.Abstractions;
using TallyG.Tax.Domain.Common;
using TallyG.Tax.Domain.Entities;
using TallyG.Tax.Domain.Enums;
using TallyG.Tax.Infrastructure.Persistence;

namespace TallyG.Tax.Api.Modules.Notices;

/// <summary>
/// Passive notice-vault implementation. Tenant + owner scoping is applied on every query
/// (a notice belongs to exactly one user). Optional scanned copies are persisted through
/// <see cref="IFileStorage"/> under a tenant/user-partitioned key. Recording a notice raises
/// an in-app notification via <see cref="INotificationService"/> so the user sees it on the
/// dashboard — demonstrating the cross-module fan-out other modules use.
/// </summary>
public sealed class NoticeService : INoticeService
{
    // Generous upload ceiling for the demo's base64-in-JSON path (decoded bytes).
    private const int MaxAttachmentBytes = 15 * 1024 * 1024;

    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IFileStorage _storage;
    private readonly INotificationService _notifications;
    private readonly IDateTime _clock;

    public NoticeService(
        AppDbContext db,
        ICurrentUser currentUser,
        IFileStorage storage,
        INotificationService notifications,
        IDateTime clock)
    {
        _db = db;
        _currentUser = currentUser;
        _storage = storage;
        _notifications = notifications;
        _clock = clock;
    }

    // ----------------------------------------------------------------- create

    public async Task<NoticeDetailDto> CreateAsync(CreateNoticeRequest request, CancellationToken ct = default)
    {
        var userId = RequireUser();

        var noticeType = (request.NoticeType ?? string.Empty).Trim();
        if (noticeType.Length == 0)
        {
            throw AppException.Validation("A notice type is required.", "VALIDATION.NOTICE_TYPE");
        }

        // If a return is referenced, it must belong to this user+tenant.
        if (request.TaxReturnId is { } returnId)
        {
            var owns = await _db.TaxReturns.AnyAsync(
                r => r.Id == returnId && r.TenantId == _currentUser.TenantId && r.UserId == userId, ct);
            if (!owns)
            {
                throw AppException.NotFound("Tax return not found.", "RETURN.NOT_FOUND");
            }
        }

        var notice = new Notice
        {
            TenantId = _currentUser.TenantId,
            UserId = userId,
            TaxReturnId = request.TaxReturnId,
            NoticeType = noticeType,
            Section = Trim(request.Section),
            Din = Trim(request.Din),
            ReceivedAt = request.ReceivedAt ?? _clock.UtcNow,
            DueDate = request.DueDate,
            Summary = Trim(request.Summary),
            DemandAmount = request.DemandAmount,
            RefundAmount = request.RefundAmount,
            Status = NoticeStatus.Open
        };

        // Optionally persist a scanned copy of the notice into the vault.
        notice.FilePath = await StoreAttachmentAsync(
            "notices", notice.Id, request.FileName, request.ContentType, request.FileBase64, ct);

        _db.Notices.Add(notice);
        await _db.SaveChangesAsync(ct);

        // In-app heads-up (best-effort; not part of the transaction's success criteria here).
        await _notifications.NotifyAsync(
            _currentUser.TenantId,
            userId,
            templateCode: "notice.recorded",
            title: $"Notice {notice.NoticeType} added",
            body: notice.DueDate is { } due
                ? $"A {notice.NoticeType} notice was added to your vault. Response due by {due:dd MMM yyyy}."
                : $"A {notice.NoticeType} notice was added to your vault.",
            channel: NotificationChannel.InApp,
            ct: ct);

        return await GetAsync(notice.Id, ct);
    }

    // ------------------------------------------------------------------- list

    public async Task<PagedResult<NoticeDto>> ListAsync(
        int page, int pageSize, string? status, CancellationToken ct = default)
    {
        var (p, size) = SupportPaging.Normalize(page, pageSize);
        var userId = RequireUser();

        var query = _db.Notices
            .AsNoTracking()
            .Where(n => n.TenantId == _currentUser.TenantId && n.UserId == userId);

        if (!string.IsNullOrWhiteSpace(status))
        {
            var parsed = ParseStatus(status);
            query = query.Where(n => n.Status == parsed);
        }

        var total = await query.LongCountAsync(ct);

        // Order/page client-side (Sqlite cannot ORDER BY DateTimeOffset; a user's notices are bounded).
        var rows = await query.ToListAsync(ct);
        var items = rows
            .OrderByDescending(n => n.ReceivedAt)
            .ThenByDescending(n => n.Id)
            .Skip((p - 1) * size)
            .Take(size)
            .Select(n => new NoticeDto(
                n.Id, n.NoticeType, n.Section, n.Din, n.TaxReturnId, n.ReceivedAt, n.DueDate,
                n.Summary, n.DemandAmount, n.RefundAmount, n.Status.ToString(),
                n.FilePath != null, n.CreatedAt))
            .ToList();

        return new PagedResult<NoticeDto>(items, p, size, total);
    }

    // -------------------------------------------------------------------- get

    public async Task<NoticeDetailDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var notice = await LoadNoticeAsync(id, ct);

        var responseRows = await _db.NoticeResponses
            .AsNoTracking()
            .Where(r => r.NoticeId == notice.Id && r.TenantId == _currentUser.TenantId)
            .ToListAsync(ct);

        // Order client-side (Sqlite cannot ORDER BY DateTimeOffset).
        var responses = responseRows
            .OrderBy(r => r.CreatedAt)
            .ThenBy(r => r.Id)
            .Select(r => new NoticeResponseDto(
                r.Id, r.ResponseText, r.ResponseType, r.FilePath != null,
                r.RespondedByUserId, r.AcknowledgementNo, r.CreatedAt))
            .ToList();

        return new NoticeDetailDto(
            notice.Id, notice.NoticeType, notice.Section, notice.Din, notice.TaxReturnId,
            notice.ReceivedAt, notice.DueDate, notice.Summary, notice.DemandAmount, notice.RefundAmount,
            notice.Status.ToString(), notice.FilePath != null, notice.CreatedAt, responses);
    }

    // -------------------------------------------------------------- responses

    public async Task<NoticeResponseDto> AddResponseAsync(
        Guid id, CreateNoticeResponseRequest request, CancellationToken ct = default)
    {
        var notice = await LoadNoticeAsync(id, ct);

        var text = (request.ResponseText ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            throw AppException.Validation("Response text is required.", "VALIDATION.NOTICE_RESPONSE");
        }

        var response = new NoticeResponse
        {
            TenantId = _currentUser.TenantId,
            NoticeId = notice.Id,
            ResponseText = text,
            ResponseType = Trim(request.ResponseType),
            RespondedByUserId = _currentUser.UserId
        };

        response.FilePath = await StoreAttachmentAsync(
            "notice-responses", response.Id, request.FileName, request.ContentType, request.FileBase64, ct);

        _db.NoticeResponses.Add(response);

        // Recording a response advances an open notice to "Responded".
        if (notice.Status is NoticeStatus.Open or NoticeStatus.InProgress)
        {
            notice.Status = NoticeStatus.Responded;
        }

        await _db.SaveChangesAsync(ct);

        return new NoticeResponseDto(
            response.Id, response.ResponseText, response.ResponseType, response.FilePath != null,
            response.RespondedByUserId, response.AcknowledgementNo, response.CreatedAt);
    }

    // ----------------------------------------------------------------- status

    public async Task<NoticeDto> UpdateStatusAsync(
        Guid id, UpdateNoticeStatusRequest request, CancellationToken ct = default)
    {
        var notice = await LoadNoticeAsync(id, ct);
        notice.Status = ParseStatus(request.Status);
        await _db.SaveChangesAsync(ct);

        return new NoticeDto(
            notice.Id, notice.NoticeType, notice.Section, notice.Din, notice.TaxReturnId,
            notice.ReceivedAt, notice.DueDate, notice.Summary, notice.DemandAmount, notice.RefundAmount,
            notice.Status.ToString(), notice.FilePath != null, notice.CreatedAt);
    }

    // ============================================================== internals

    private async Task<Notice> LoadNoticeAsync(Guid id, CancellationToken ct)
    {
        var notice = await _db.Notices.FirstOrDefaultAsync(n => n.Id == id, ct);

        // Out-of-tenant or another user's notice → 404 (never leak existence).
        if (notice is null || notice.TenantId != _currentUser.TenantId || notice.UserId != _currentUser.UserId)
        {
            throw AppException.NotFound("Notice not found.", "NOTICE.NOT_FOUND");
        }

        return notice;
    }

    /// <summary>
    /// Decode an optional base64 attachment and persist it via IFileStorage, returning the
    /// storage key (or null when no file was supplied). Keys are partitioned by tenant/user.
    /// </summary>
    private async Task<string?> StoreAttachmentAsync(
        string folder, Guid ownerId, string? fileName, string? contentType, string? base64, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(base64))
        {
            return null;
        }

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(base64);
        }
        catch (FormatException)
        {
            throw AppException.Validation("Attachment must be valid base64.", "VALIDATION.NOTICE_FILE");
        }

        if (bytes.Length == 0)
        {
            return null;
        }

        if (bytes.Length > MaxAttachmentBytes)
        {
            throw AppException.Validation(
                $"Attachment exceeds the {MaxAttachmentBytes / (1024 * 1024)} MB limit.", "VALIDATION.NOTICE_FILE");
        }

        var safeName = SanitizeFileName(fileName) ?? $"{ownerId}.bin";
        var storageKey = $"{folder}/{_currentUser.TenantId}/{_currentUser.UserId}/{ownerId}/{safeName}";

        await _storage.SaveAsync(
            storageKey, bytes, string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType, ct);

        return storageKey;
    }

    private Guid RequireUser()
    {
        if (!_currentUser.IsAuthenticated)
        {
            throw new AppException("AUTH.UNAUTHENTICATED", "Not authenticated.", 401);
        }

        return _currentUser.UserId;
    }

    private static string? Trim(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? SanitizeFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        // Strip any path components and disallowed characters; never trust client file names.
        var name = Path.GetFileName(fileName.Trim());
        var cleaned = new string(name.Where(c => char.IsLetterOrDigit(c) || c is '.' or '-' or '_').ToArray());
        return cleaned.Length == 0 ? null : cleaned;
    }

    private static NoticeStatus ParseStatus(string? status) =>
        (status ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "open" => NoticeStatus.Open,
            "inprogress" or "in_progress" => NoticeStatus.InProgress,
            "responded" => NoticeStatus.Responded,
            "closed" => NoticeStatus.Closed,
            "escalated" => NoticeStatus.Escalated,
            _ => throw AppException.Validation(
                $"Unsupported notice status '{status}'.", "VALIDATION.NOTICE_STATUS")
        };
}
