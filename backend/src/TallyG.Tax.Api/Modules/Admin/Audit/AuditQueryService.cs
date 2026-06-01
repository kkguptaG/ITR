using Microsoft.EntityFrameworkCore;
using TallyG.Tax.Domain.Abstractions;
using TallyG.Tax.Domain.Common;
using TallyG.Tax.Domain.Entities;
using TallyG.Tax.Infrastructure.Persistence;

namespace TallyG.Tax.Api.Modules.Admin.Audit;

/// <summary>
/// Audit-trail read service. Admin/Ops see entries scoped to their tenant; SuperAdmin sees all
/// tenants plus null-tenant system events. Filters (actor/entityType/entityId/action) are pushed
/// to the database; actor display names are resolved in a single follow-up round-trip.
/// No manual DI — Scrutor binds AuditQueryService : IAuditQueryService scoped.
/// </summary>
public sealed class AuditQueryService : IAuditQueryService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public AuditQueryService(AppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<AuditLogDto>> ListAsync(
        Guid? actorUserId,
        string? entityType,
        Guid? entityId,
        string? action,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        (page, pageSize) = AdminPaging.Normalize(page, pageSize);

        var query = _db.AuditLogs.AsQueryable();

        if (!AdminScope.IsCrossTenant(_currentUser))
        {
            // Tenant admins see their tenant's events only (system/null-tenant events are hidden).
            var tenantId = _currentUser.TenantId;
            query = query.Where(a => a.TenantId == tenantId);
        }

        if (actorUserId is { } actor)
        {
            query = query.Where(a => a.ActorUserId == actor);
        }

        if (!string.IsNullOrWhiteSpace(entityType))
        {
            var et = entityType.Trim();
            query = query.Where(a => a.EntityType == et);
        }

        if (entityId is { } eid)
        {
            query = query.Where(a => a.EntityId == eid);
        }

        if (!string.IsNullOrWhiteSpace(action))
        {
            var prefix = action.Trim();
            query = query.Where(a => a.Action.StartsWith(prefix));
        }

        var total = await query.LongCountAsync(ct);

        var logs = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var actorIds = logs
            .Where(l => l.ActorUserId is not null)
            .Select(l => l.ActorUserId!.Value)
            .Distinct()
            .ToArray();

        var actorNames = actorIds.Length == 0
            ? new Dictionary<Guid, string>()
            : await _db.Users
                .Where(u => actorIds.Contains(u.Id))
                .Select(u => new { u.Id, u.FullName })
                .ToDictionaryAsync(x => x.Id, x => x.FullName, ct);

        var items = logs
            .Select(l => new AuditLogDto(
                l.Id, l.TenantId, l.ActorUserId,
                l.ActorUserId is { } a && actorNames.TryGetValue(a, out var n) ? n : null,
                l.Action, l.EntityType, l.EntityId, l.DataJson, l.IpAddress, l.UserAgent, l.CreatedAt))
            .ToList();

        return new PagedResult<AuditLogDto>(items, page, pageSize, total);
    }
}
