using TallyG.Tax.Domain.Common;

namespace TallyG.Tax.Api.Modules.Ca;

/// <summary>
/// In-house CA review workflow (Ch.4 §"CA Workflow", Ch.7 S6). Drives a return through
/// assignment → review → approve/return-for-fix, enforcing the §4.5 ownership/assignment
/// rule (only the assigned CA, or an Ops/Admin/CaFirmAdmin operator, may act).
/// </summary>
public interface ICaService
{
    /// <summary>
    /// The current principal's work queue. CA/Reviewer see returns assigned to them;
    /// CaFirmAdmin/Ops/Admin additionally see the unassigned <c>UnderCaReview</c> pool.
    /// </summary>
    Task<PagedResult<QueueItemDto>> GetQueueAsync(int page, int pageSize, CancellationToken ct = default);

    /// <summary>Assign a return to a CA (Ops/Admin/CaFirmAdmin); moves the return to UnderCaReview.</summary>
    Task<AssignmentDto> AssignAsync(Guid returnId, AssignReturnRequest request, CancellationToken ct = default);

    /// <summary>Approve a return under review; completes the assignment and sets ReadyToFile.</summary>
    Task<AssignmentDto> ApproveAsync(Guid returnId, ReviewActionRequest request, CancellationToken ct = default);

    /// <summary>Send a return back to the taxpayer with notes; sets InProgress and notifies the user.</summary>
    Task<AssignmentDto> RequestChangesAsync(Guid returnId, ReviewActionRequest request, CancellationToken ct = default);

    /// <summary>Assignment detail with the return summary and full comment history.</summary>
    Task<AssignmentDetailDto> GetAssignmentAsync(Guid assignmentId, CancellationToken ct = default);
}
