// Admin/CRM module — user-administration DTOs.
// Public contract for the back-office user board (docs 04 §"Admin/CRM", docs 07 §7.8 admin console).
// JSON is camelCase on the wire (ASP.NET Core default), mapping to these PascalCase records.

using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Api.Modules.Admin.Users;

/// <summary>One row in the admin user list (GET /admin/users). PAN is never exposed beyond its mask.</summary>
public sealed record AdminUserListItemDto(
    Guid Id,
    Guid TenantId,
    string FullName,
    string? Email,
    string? Mobile,
    string? PanMasked,
    UserStatus Status,
    bool EmailVerified,
    bool MobileVerified,
    IReadOnlyList<string> Roles,
    DateTimeOffset? LastLoginAt,
    DateTimeOffset CreatedAt);

/// <summary>Full admin view of a single user (GET /admin/users/{id}) with activity counters.</summary>
public sealed record AdminUserDetailDto(
    Guid Id,
    Guid TenantId,
    string FullName,
    string? Email,
    string? Mobile,
    string? PanMasked,
    UserStatus Status,
    bool EmailVerified,
    bool MobileVerified,
    IReadOnlyList<string> Roles,
    int ReturnsCount,
    int PaymentsCount,
    DateTimeOffset? LastLoginAt,
    DateTimeOffset CreatedAt);

/// <summary>
/// PATCH /admin/users/{id}:status body — set the account state. Only "Active", "Locked" and
/// "Disabled" are settable through this endpoint; hard "Deleted" goes through the DPDP erasure
/// path, not here.
/// </summary>
public sealed record UpdateUserStatusRequest(UserStatus Status, string? Reason = null);

/// <summary>
/// POST /admin/users/{id}/roles body — grant or revoke a single role for the user.
/// <see cref="Action"/> is "assign" (default) or "remove"; <see cref="Role"/> is the role name
/// (e.g. "CA", "Ops"). The grant is unscoped (tenant-wide) for the demo.
/// </summary>
public sealed record ModifyUserRoleRequest(string Role, string Action = "assign");
