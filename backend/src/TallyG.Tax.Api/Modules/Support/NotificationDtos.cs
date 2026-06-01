// Notifications module — request/response DTOs.
// JSON is camelCase on the wire (ASP.NET Core default), mapping to these PascalCase records.

namespace TallyG.Tax.Api.Modules.Support;

/// <summary>A single in-app notification surfaced to the current user.</summary>
public sealed record NotificationDto(
    Guid Id,
    string Channel,
    string Template,
    string? Title,
    string? Body,
    string Status,
    bool IsRead,
    DateTimeOffset? ReadAt,
    DateTimeOffset? SentAt,
    DateTimeOffset CreatedAt);

/// <summary>
/// POST /notifications:mark-read body. When <see cref="Ids"/> is null or empty, every
/// unread notification for the current user is marked read.
/// </summary>
public sealed record MarkNotificationsReadRequest(IReadOnlyList<Guid>? Ids);

/// <summary>POST /notifications:mark-read response: how many rows transitioned to read.</summary>
public sealed record MarkNotificationsReadResponse(int MarkedRead);
