using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TallyG.Tax.Domain.Common;

namespace TallyG.Tax.Api.Modules.Admin.Returns;

/// <summary>
/// Back-office filing board + document-verification (HITL) queue (docs 04 §"Admin/CRM",
/// docs 05 §5.2, docs 07 §7.8). Admin/Ops within their tenant; SuperAdmin across all.
/// The CA-assignment action uses the canonical ":assign-ca" sub-resource verb (Decision Log D-3).
/// </summary>
[ApiController]
[Route("api/v1/admin")]
[Authorize(Roles = AdminScope.Roles)]
public sealed class AdminReturnsController : ControllerBase
{
    private readonly IAdminReturnService _returns;

    public AdminReturnsController(IAdminReturnService returns) => _returns = returns;

    /// <summary>The returns board, optionally filtered by status (e.g. ?status=UnderCaReview).</summary>
    [HttpGet("returns")]
    [ProducesResponseType(typeof(PagedResult<AdminReturnListItemDto>), StatusCodes.Status200OK)]
    public Task<PagedResult<AdminReturnListItemDto>> List(
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = AdminPaging.DefaultPageSize,
        CancellationToken ct = default)
        => _returns.ListAsync(status, page, pageSize, ct);

    /// <summary>Route a return to a CA for review.</summary>
    [HttpPost("returns/{id:guid}:assign-ca")]
    [ProducesResponseType(typeof(AdminAssignmentResultDto), StatusCodes.Status200OK)]
    public Task<AdminAssignmentResultDto> AssignCa(
        [FromRoute] Guid id,
        [FromBody] AssignReturnToCaRequest request,
        CancellationToken ct)
        => _returns.AssignCaAsync(id, request, ct);

    /// <summary>Documents awaiting human verification (extraction confidence below threshold).</summary>
    [HttpGet("doc-verification-queue")]
    [ProducesResponseType(typeof(PagedResult<DocVerificationQueueItemDto>), StatusCodes.Status200OK)]
    public Task<PagedResult<DocVerificationQueueItemDto>> DocVerificationQueue(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = AdminPaging.DefaultPageSize,
        CancellationToken ct = default)
        => _returns.GetDocVerificationQueueAsync(page, pageSize, ct);
}
