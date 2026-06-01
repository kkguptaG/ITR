namespace TallyG.Tax.Domain.Entities;

/// <summary>Join row granting a <see cref="Role"/> to a <see cref="User"/> (composite PK).</summary>
public class UserRole
{
    public Guid UserId { get; set; }
    public Guid RoleId { get; set; }

    /// <summary>
    /// Sub-tenant scope for a CA-firm admin's grant. <see cref="Guid.Empty"/> means an
    /// unscoped (global within the tenant) grant. Kept non-nullable because it is part of
    /// the composite primary key (EF Core disallows nullable key columns).
    /// </summary>
    public Guid ScopeTenantId { get; set; } = Guid.Empty;

    public Guid? GrantedBy { get; set; }
    public DateTimeOffset GrantedAt { get; set; } = DateTimeOffset.UtcNow;

    public User? User { get; set; }
    public Role? Role { get; set; }
}
