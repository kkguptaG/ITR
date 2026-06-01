using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TallyG.Tax.Domain.Common;

namespace TallyG.Tax.Api.Modules.Ca;

/// <summary>
/// In-house CA review workflow (Ch.4 §"CA Workflow", Ch.7 S6). Thin actions delegating to
/// <see cref="ICaService"/>. Routes use the canonical ":verb" sub-resource convention for
/// actions (Decision Log D-3). Role gating is the coarse first layer; the service enforces the
/// finer §4.5 ownership/assignment rule (only the assigned CA or an operator may act).
/// </summary>
[ApiController]
[Route("api/v1")]
[Authorize]
public sealed class CaController : ControllerBase
{
    private readonly ICaService _ca;

    public CaController(ICaService ca) => _ca = ca;

    /// <summary>
    /// The caller's CA work queue. CA/Reviewer get returns assigned to them; CaFirmAdmin also
    /// sees the unassigned UnderCaReview pool for the firm/tenant.
    /// </summary>
    [HttpGet("ca/queue")]
    [Authorize(Roles = "CA,CaFirmAdmin,Reviewer")]
    [ProducesResponseType(typeof(PagedResult<QueueItemDto>), StatusCodes.Status200OK)]
    public Task<PagedResult<QueueItemDto>> Queue(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => _ca.GetQueueAsync(page, pageSize, ct);

    /// <summary>Assign a return to a CA. Operator action (Ops/Admin/CaFirmAdmin).</summary>
    [HttpPost("returns/{id:guid}/assignment")]
    [Authorize(Roles = "Ops,Admin,CaFirmAdmin,SuperAdmin")]
    [ProducesResponseType(typeof(AssignmentDto), StatusCodes.Status200OK)]
    public Task<AssignmentDto> Assign(
        [FromRoute] Guid id,
        [FromBody] AssignReturnRequest request,
        CancellationToken ct)
        => _ca.AssignAsync(id, request, ct);

    /// <summary>Approve a return under review → assignment Completed, return ReadyToFile.</summary>
    [HttpPost("returns/{id:guid}/review:approve")]
    [Authorize(Roles = "CA,CaFirmAdmin,Reviewer")]
    [ProducesResponseType(typeof(AssignmentDto), StatusCodes.Status200OK)]
    public Task<AssignmentDto> Approve(
        [FromRoute] Guid id,
        [FromBody] ReviewActionRequest request,
        CancellationToken ct)
        => _ca.ApproveAsync(id, request, ct);

    /// <summary>Send a return back to the taxpayer with notes → return InProgress; user notified.</summary>
    [HttpPost("returns/{id:guid}/review:request-changes")]
    [Authorize(Roles = "CA,CaFirmAdmin,Reviewer")]
    [ProducesResponseType(typeof(AssignmentDto), StatusCodes.Status200OK)]
    public Task<AssignmentDto> RequestChanges(
        [FromRoute] Guid id,
        [FromBody] ReviewActionRequest request,
        CancellationToken ct)
        => _ca.RequestChangesAsync(id, request, ct);

    /// <summary>Assignment detail: the return summary plus the full review comment history.</summary>
    [HttpGet("ca/assignments/{id:guid}")]
    [Authorize(Roles = "CA,CaFirmAdmin,Reviewer,Ops,Admin,SuperAdmin")]
    [ProducesResponseType(typeof(AssignmentDetailDto), StatusCodes.Status200OK)]
    public Task<AssignmentDetailDto> GetAssignment([FromRoute] Guid id, CancellationToken ct)
        => _ca.GetAssignmentAsync(id, ct);
}
