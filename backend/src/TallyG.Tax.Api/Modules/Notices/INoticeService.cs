using TallyG.Tax.Domain.Common;

namespace TallyG.Tax.Api.Modules.Notices;

/// <summary>
/// Passive ITD-notice vault for the current user (Decision Log D-6; docs 04 §4.2 Notices).
/// Users record notices and responses and track status manually — no automated fetch in V1.
/// Everything is scoped to the authenticated user + tenant. Auto-registered scoped by Scrutor.
/// </summary>
public interface INoticeService
{
    /// <summary>Record a notice (and optionally store a scanned copy via IFileStorage).</summary>
    Task<NoticeDetailDto> CreateAsync(CreateNoticeRequest request, CancellationToken ct = default);

    /// <summary>List the current user's notices (newest received first, paged).</summary>
    Task<PagedResult<NoticeDto>> ListAsync(
        int page, int pageSize, string? status, CancellationToken ct = default);

    /// <summary>Get a notice with its recorded responses.</summary>
    Task<NoticeDetailDto> GetAsync(Guid id, CancellationToken ct = default);

    /// <summary>Record a response against a notice (optionally with an attachment).</summary>
    Task<NoticeResponseDto> AddResponseAsync(
        Guid id, CreateNoticeResponseRequest request, CancellationToken ct = default);

    /// <summary>Update the manual status of a notice.</summary>
    Task<NoticeDto> UpdateStatusAsync(
        Guid id, UpdateNoticeStatusRequest request, CancellationToken ct = default);
}
