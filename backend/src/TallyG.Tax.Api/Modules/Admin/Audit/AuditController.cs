using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TallyG.Tax.Domain.Common;

namespace TallyG.Tax.Api.Modules.Admin.Audit;

/// <summary>
/// Audit-trail viewer (docs 04/06 §"Audit & PII-access logs"). Admin/Ops see their tenant's events;
/// SuperAdmin sees all tenants plus system events. Read-only — the trail is append-only and is
/// written via <see cref="IAuditWriterService"/> from across the modules.
/// </summary>
[ApiController]
[Route("api/v1/admin/audit")]
[Authorize(Roles = AdminScope.Roles)]
public sealed class AuditController : ControllerBase
{
    private readonly IAuditQueryService _audit;

    public AuditController(IAuditQueryService audit) => _audit = audit;

    /// <summary>Paged audit entries (newest first), filterable by actor, entity and action prefix.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<AuditLogDto>), StatusCodes.Status200OK)]
    public Task<PagedResult<AuditLogDto>> List(
        [FromQuery] Guid? actorUserId = null,
        [FromQuery] string? entityType = null,
        [FromQuery] Guid? entityId = null,
        [FromQuery] string? action = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = AdminPaging.DefaultPageSize,
        CancellationToken ct = default)
        => _audit.ListAsync(actorUserId, entityType, entityId, action, page, pageSize, ct);
}
