using Microsoft.EntityFrameworkCore;
using TallyG.Tax.Api.Modules.Admin.Audit;
using TallyG.Tax.Domain.Abstractions;
using TallyG.Tax.Domain.Common;
using TallyG.Tax.Domain.Entities;
using TallyG.Tax.Domain.Enums;
using TallyG.Tax.Infrastructure.Persistence;

namespace TallyG.Tax.Api.Modules.Admin.Users;

/// <summary>
/// Back-office user administration. Scoped to the caller's tenant for Admin/Ops; SuperAdmin reads
/// across tenants. Role grants are guarded so only a SuperAdmin can mint Admin/SuperAdmin (no
/// privilege escalation by a tenant Admin). Every mutation is recorded to the audit trail.
/// No manual DI — Scrutor binds AdminUserService : IAdminUserService scoped.
/// </summary>
public sealed class AdminUserService : IAdminUserService
{
    // Roles whose grant/revoke is reserved to a SuperAdmin (privilege-escalation guard).
    private static readonly HashSet<string> PrivilegedRoles =
        new(StringComparer.OrdinalIgnoreCase) { "Admin", "SuperAdmin" };

    // Status values an operator may set here; hard-delete is out of band (DPDP erasure).
    private static readonly HashSet<UserStatus> SettableStatuses =
        new() { UserStatus.Active, UserStatus.Locked, UserStatus.Disabled };

    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditWriterService _audit;
    private readonly ILogger<AdminUserService> _logger;

    public AdminUserService(
        AppDbContext db,
        ICurrentUser currentUser,
        IAuditWriterService audit,
        ILogger<AdminUserService> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _audit = audit;
        _logger = logger;
    }

    // -------------------------------------------------------------------- list

    public async Task<PagedResult<AdminUserListItemDto>> ListAsync(
        string? search, int page, int pageSize, CancellationToken ct = default)
    {
        (page, pageSize) = AdminPaging.Normalize(page, pageSize);

        var query = ScopedUsers();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            // Case-insensitive contains on name/email/mobile. EF.Functions.Like keeps it
            // translatable on both Sqlite and Npgsql; we lower-case both sides for portability.
            var like = $"%{term.ToLowerInvariant()}%";
            query = query.Where(u =>
                EF.Functions.Like(u.FullName.ToLower(), like)
                || (u.Email != null && EF.Functions.Like(u.Email.ToLower(), like))
                || (u.MobileE164 != null && EF.Functions.Like(u.MobileE164, $"%{term}%")));
        }

        var total = await query.LongCountAsync(ct);

        var users = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var roleMap = await ResolveRolesAsync(users.Select(u => u.Id), ct);

        var items = users
            .Select(u => new AdminUserListItemDto(
                u.Id, u.TenantId, u.FullName, u.Email, u.MobileE164, u.PanMasked,
                u.Status, u.EmailVerified, u.MobileVerified,
                roleMap.TryGetValue(u.Id, out var roles) ? roles : Array.Empty<string>(),
                u.LastLoginAt, u.CreatedAt))
            .ToList();

        return new PagedResult<AdminUserListItemDto>(items, page, pageSize, total);
    }

    // ------------------------------------------------------------------- detail

    public async Task<AdminUserDetailDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var user = await LoadUserAsync(id, ct);
        return await BuildDetailAsync(user, ct);
    }

    // ------------------------------------------------------------------- status

    public async Task<AdminUserDetailDto> SetStatusAsync(
        Guid id, UpdateUserStatusRequest request, CancellationToken ct = default)
    {
        if (!SettableStatuses.Contains(request.Status))
        {
            throw AppException.Validation(
                "Only Active, Locked or Disabled can be set here.", "ADMIN.STATUS_NOT_SETTABLE");
        }

        var user = await LoadUserAsync(id, ct);

        // Guard against an operator locking themselves out of the console.
        if (user.Id == _currentUser.UserId && request.Status != UserStatus.Active)
        {
            throw AppException.Validation(
                "You cannot change the status of your own account.", "ADMIN.SELF_STATUS_CHANGE");
        }

        var previous = user.Status;
        if (previous != request.Status)
        {
            user.Status = request.Status;

            _audit.Write("admin.user.status_changed", nameof(User), user.Id, new
            {
                from = previous.ToString(),
                to = request.Status.ToString(),
                reason = request.Reason,
                by = _currentUser.UserId
            });

            await _db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "User {UserId} status {From} -> {To} by {ActorId}",
                user.Id, previous, request.Status, _currentUser.UserId);
        }

        return await BuildDetailAsync(user, ct);
    }

    // -------------------------------------------------------------------- roles

    public async Task<AdminUserDetailDto> ModifyRoleAsync(
        Guid id, ModifyUserRoleRequest request, CancellationToken ct = default)
    {
        var roleName = (request.Role ?? string.Empty).Trim();
        if (roleName.Length == 0)
        {
            throw AppException.Validation("A role name is required.", "ADMIN.ROLE_REQUIRED");
        }

        var remove = string.Equals(request.Action, "remove", StringComparison.OrdinalIgnoreCase);
        if (!remove && !string.Equals(request.Action, "assign", StringComparison.OrdinalIgnoreCase))
        {
            throw AppException.Validation(
                "Action must be 'assign' or 'remove'.", "ADMIN.ROLE_ACTION_INVALID");
        }

        // Only a SuperAdmin may grant/revoke the privileged roles.
        if (PrivilegedRoles.Contains(roleName) && !_currentUser.IsInRole("SuperAdmin"))
        {
            throw AppException.Forbidden(
                "Only a SuperAdmin can change Admin/SuperAdmin roles.", "ADMIN.ROLE_PRIVILEGED");
        }

        var user = await LoadUserAsync(id, ct);

        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Name == roleName, ct)
                   ?? throw AppException.NotFound($"Role '{roleName}' does not exist.", "ADMIN.ROLE_NOT_FOUND");

        var existing = await _db.UserRoles.FirstOrDefaultAsync(
            ur => ur.UserId == user.Id && ur.RoleId == role.Id && ur.ScopeTenantId == Guid.Empty, ct);

        if (remove)
        {
            if (existing is null)
            {
                throw AppException.Conflict(
                    "The user does not have that role.", "ADMIN.ROLE_NOT_ASSIGNED");
            }

            // Do not strip the caller's own last privileged role (lock-out guard).
            if (user.Id == _currentUser.UserId && PrivilegedRoles.Contains(roleName))
            {
                throw AppException.Validation(
                    "You cannot remove your own privileged role.", "ADMIN.SELF_ROLE_REMOVE");
            }

            _db.UserRoles.Remove(existing);
            _audit.Write("admin.user.role_removed", nameof(User), user.Id,
                new { role = roleName, by = _currentUser.UserId });
        }
        else
        {
            if (existing is not null)
            {
                // Idempotent: granting an already-held role is a no-op (return current state).
                return await BuildDetailAsync(user, ct);
            }

            _db.UserRoles.Add(new UserRole
            {
                UserId = user.Id,
                RoleId = role.Id,
                ScopeTenantId = Guid.Empty,
                GrantedBy = _currentUser.UserId,
                GrantedAt = DateTimeOffset.UtcNow
            });
            _audit.Write("admin.user.role_assigned", nameof(User), user.Id,
                new { role = roleName, by = _currentUser.UserId });
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "User {UserId} role '{Role}' {Action} by {ActorId}",
            user.Id, roleName, remove ? "removed" : "assigned", _currentUser.UserId);

        return await BuildDetailAsync(user, ct);
    }

    // ============================================================== internals

    /// <summary>Users visible to the caller: own-tenant for Admin/Ops, all for SuperAdmin.</summary>
    private IQueryable<User> ScopedUsers()
    {
        var query = _db.Users.AsQueryable();
        if (!AdminScope.IsCrossTenant(_currentUser))
        {
            var tenantId = _currentUser.TenantId;
            query = query.Where(u => u.TenantId == tenantId);
        }

        return query;
    }

    private async Task<User> LoadUserAsync(Guid id, CancellationToken ct)
        => await ScopedUsers().FirstOrDefaultAsync(u => u.Id == id, ct)
           // Cross-tenant / soft-deleted users read as "missing" (no enumeration leak).
           ?? throw AppException.NotFound("User not found.", "ADMIN.USER_NOT_FOUND");

    private async Task<AdminUserDetailDto> BuildDetailAsync(User user, CancellationToken ct)
    {
        var roleMap = await ResolveRolesAsync(new[] { user.Id }, ct);
        var roles = roleMap.TryGetValue(user.Id, out var r) ? r : Array.Empty<string>();

        var returnsCount = await _db.TaxReturns.CountAsync(t => t.UserId == user.Id, ct);
        var paymentsCount = await _db.Payments.CountAsync(p => p.UserId == user.Id, ct);

        return new AdminUserDetailDto(
            user.Id, user.TenantId, user.FullName, user.Email, user.MobileE164, user.PanMasked,
            user.Status, user.EmailVerified, user.MobileVerified, roles,
            returnsCount, paymentsCount, user.LastLoginAt, user.CreatedAt);
    }

    /// <summary>Resolve role names for a batch of users in a single round-trip.</summary>
    private async Task<Dictionary<Guid, string[]>> ResolveRolesAsync(
        IEnumerable<Guid> userIds, CancellationToken ct)
    {
        var ids = userIds.Distinct().ToArray();
        if (ids.Length == 0)
        {
            return new Dictionary<Guid, string[]>();
        }

        var pairs = await _db.UserRoles
            .Where(ur => ids.Contains(ur.UserId))
            .Join(_db.Roles, ur => ur.RoleId, role => role.Id, (ur, role) => new { ur.UserId, role.Name })
            .ToListAsync(ct);

        return pairs
            .GroupBy(p => p.UserId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => x.Name).Distinct().OrderBy(n => n).ToArray());
    }
}
