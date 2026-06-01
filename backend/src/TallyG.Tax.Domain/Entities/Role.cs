using TallyG.Tax.Domain.Common;

namespace TallyG.Tax.Domain.Entities;

/// <summary>An RBAC role (User, CA, CaFirmAdmin, Reviewer, Ops, Admin, SuperAdmin, Affiliate).</summary>
public class Role : BaseEntity
{
    /// <summary>Stable machine code, e.g. "Admin". Used in JWT "role" claims and [Authorize(Roles=...)].</summary>
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>System roles are seeded and not tenant-specific.</summary>
    public bool IsSystem { get; set; } = true;

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}
