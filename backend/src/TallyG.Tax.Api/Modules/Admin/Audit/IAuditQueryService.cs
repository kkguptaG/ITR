using TallyG.Tax.Domain.Common;

namespace TallyG.Tax.Api.Modules.Admin.Audit;

/// <summary>
/// Read side of the audit trail (docs 04/06). Pages the immutable <c>AuditLogs</c>, filterable by
/// actor, entity type and entity id. Tenant-scoped for Admin/Ops; all-tenant (incl. system events)
/// for SuperAdmin. Auto-registered scoped (AuditQueryService : IAuditQueryService).
/// </summary>
public interface IAuditQueryService
{
    /// <summary>
    /// GET /admin/audit — paged audit entries, newest first. All filters are optional and ANDed:
    /// <paramref name="actorUserId"/>, <paramref name="entityType"/> (case-insensitive),
    /// <paramref name="entityId"/>, and an optional <paramref name="action"/> prefix.
    /// </summary>
    Task<PagedResult<AuditLogDto>> ListAsync(
        Guid? actorUserId,
        string? entityType,
        Guid? entityId,
        string? action,
        int page,
        int pageSize,
        CancellationToken ct = default);
}
