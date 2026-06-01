using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TallyG.Tax.Api.Modules.Support;
using TallyG.Tax.Domain.Common;

namespace TallyG.Tax.Api.Modules.Notices;

/// <summary>
/// ITD notices vault (Decision Log D-6 — passive V1 vault; docs 04 §4.2 Notices).
/// All actions are scoped to the authenticated user + tenant. Status uses the canonical
/// ":status" action sub-resource convention.
/// </summary>
[ApiController]
[Route("api/v1/notices")]
[Authorize]
public sealed class NoticesController : ControllerBase
{
    private readonly INoticeService _notices;

    public NoticesController(INoticeService notices) => _notices = notices;

    /// <summary>Record a notice (metadata + optional scanned copy).</summary>
    [HttpPost]
    [ProducesResponseType(typeof(NoticeDetailDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateNoticeRequest request, CancellationToken ct)
    {
        var notice = await _notices.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = notice.Id }, notice);
    }

    /// <summary>List the current user's notices (newest received first, paged).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<NoticeDto>), StatusCodes.Status200OK)]
    public Task<PagedResult<NoticeDto>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = SupportPaging.DefaultPageSize,
        [FromQuery] string? status = null,
        CancellationToken ct = default)
        => _notices.ListAsync(page, pageSize, status, ct);

    /// <summary>Get a notice with its recorded responses.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(NoticeDetailDto), StatusCodes.Status200OK)]
    public Task<NoticeDetailDto> Get(Guid id, CancellationToken ct) => _notices.GetAsync(id, ct);

    /// <summary>Record a response against a notice (optionally with an attachment).</summary>
    [HttpPost("{id:guid}/responses")]
    [ProducesResponseType(typeof(NoticeResponseDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> AddResponse(
        Guid id, [FromBody] CreateNoticeResponseRequest request, CancellationToken ct)
    {
        var response = await _notices.AddResponseAsync(id, request, ct);
        return CreatedAtAction(nameof(Get), new { id }, response);
    }

    /// <summary>Update the manual status of a notice (Open, InProgress, Responded, Closed, Escalated).</summary>
    [HttpPatch("{id:guid}:status")]
    [ProducesResponseType(typeof(NoticeDto), StatusCodes.Status200OK)]
    public Task<NoticeDto> UpdateStatus(
        Guid id, [FromBody] UpdateNoticeStatusRequest request, CancellationToken ct)
        => _notices.UpdateStatusAsync(id, request, ct);
}
