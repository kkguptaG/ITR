using TallyG.Tax.Domain.Common;

namespace TallyG.Tax.Api.Modules.Admin.Users;

/// <summary>
/// Back-office user administration (docs 04 §"Admin/CRM"; docs 07 §7.8). Lists/searches users,
/// flips account status, and grants/revokes roles. Tenant-scoped for Admin/Ops, all-tenant for
/// SuperAdmin. Auto-registered scoped by Scrutor (AdminUserService : IAdminUserService).
/// </summary>
public interface IAdminUserService
{
    /// <summary>GET /admin/users — paged user board, optional free-text search over name/email/mobile.</summary>
    Task<PagedResult<AdminUserListItemDto>> ListAsync(string? search, int page, int pageSize, CancellationToken ct = default);

    /// <summary>GET /admin/users/{id} — full user detail with activity counters.</summary>
    Task<AdminUserDetailDto> GetAsync(Guid id, CancellationToken ct = default);

    /// <summary>PATCH /admin/users/{id}:status — set Active/Locked/Disabled.</summary>
    Task<AdminUserDetailDto> SetStatusAsync(Guid id, UpdateUserStatusRequest request, CancellationToken ct = default);

    /// <summary>POST /admin/users/{id}/roles — assign or remove a role (returns the refreshed detail).</summary>
    Task<AdminUserDetailDto> ModifyRoleAsync(Guid id, ModifyUserRoleRequest request, CancellationToken ct = default);
}
