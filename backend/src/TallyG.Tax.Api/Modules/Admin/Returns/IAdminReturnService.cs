using TallyG.Tax.Domain.Common;

namespace TallyG.Tax.Api.Modules.Admin.Returns;

/// <summary>
/// Back-office returns board + document-verification queue (docs 04 §"Admin/CRM"; HITL review
/// docs 05 §5.2). Tenant-scoped for Admin/Ops, all-tenant for SuperAdmin. CA assignment delegates
/// to the CA-workflow service so the assignment rules stay in one place. Auto-registered scoped.
/// </summary>
public interface IAdminReturnService
{
    /// <summary>GET /admin/returns — paged board, optional status filter (enum name, case-insensitive).</summary>
    Task<PagedResult<AdminReturnListItemDto>> ListAsync(string? status, int page, int pageSize, CancellationToken ct = default);

    /// <summary>POST /admin/returns/{id}:assign-ca — route a return to a CA for review.</summary>
    Task<AdminAssignmentResultDto> AssignCaAsync(Guid returnId, AssignReturnToCaRequest request, CancellationToken ct = default);

    /// <summary>GET /admin/doc-verification-queue — documents in NeedsReview (HITL), paged.</summary>
    Task<PagedResult<DocVerificationQueueItemDto>> GetDocVerificationQueueAsync(int page, int pageSize, CancellationToken ct = default);
}
