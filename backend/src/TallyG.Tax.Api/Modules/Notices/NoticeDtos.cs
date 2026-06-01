// Notices module — request/response DTOs.
// V1 passive vault (Decision Log D-6): users upload a notice's metadata (+ an optional
// scanned copy) and track status manually. No auto-fetch/automation in V1.

namespace TallyG.Tax.Api.Modules.Notices;

/// <summary>
/// POST /notices body. The optional <see cref="FileBase64"/>/<see cref="FileName"/> pair
/// stores a scanned copy of the notice via IFileStorage (passive vault). Money fields are
/// optional and supplied by the user from the notice text.
/// </summary>
public sealed record CreateNoticeRequest(
    string NoticeType,
    string? Section,
    string? Din,
    Guid? TaxReturnId,
    DateTimeOffset? ReceivedAt,
    DateOnly? DueDate,
    string? Summary,
    decimal? DemandAmount,
    decimal? RefundAmount,
    string? FileName,
    string? ContentType,
    string? FileBase64);

/// <summary>POST /notices/{id}/responses body.</summary>
public sealed record CreateNoticeResponseRequest(
    string ResponseText,
    string? ResponseType,
    string? FileName,
    string? ContentType,
    string? FileBase64);

/// <summary>PATCH /notices/{id}:status body. Status is one of: Open, InProgress, Responded, Closed, Escalated.</summary>
public sealed record UpdateNoticeStatusRequest(string Status);

/// <summary>A response recorded against a notice.</summary>
public sealed record NoticeResponseDto(
    Guid Id,
    string ResponseText,
    string? ResponseType,
    bool HasAttachment,
    Guid? RespondedByUserId,
    string? AcknowledgementNo,
    DateTimeOffset CreatedAt);

/// <summary>Notice summary used in list responses.</summary>
public sealed record NoticeDto(
    Guid Id,
    string NoticeType,
    string? Section,
    string? Din,
    Guid? TaxReturnId,
    DateTimeOffset ReceivedAt,
    DateOnly? DueDate,
    string? Summary,
    decimal? DemandAmount,
    decimal? RefundAmount,
    string Status,
    bool HasAttachment,
    DateTimeOffset CreatedAt);

/// <summary>Notice detail including its recorded responses.</summary>
public sealed record NoticeDetailDto(
    Guid Id,
    string NoticeType,
    string? Section,
    string? Din,
    Guid? TaxReturnId,
    DateTimeOffset ReceivedAt,
    DateOnly? DueDate,
    string? Summary,
    decimal? DemandAmount,
    decimal? RefundAmount,
    string Status,
    bool HasAttachment,
    DateTimeOffset CreatedAt,
    IReadOnlyList<NoticeResponseDto> Responses);
