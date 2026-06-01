namespace TallyG.Tax.Domain.Entities;

/// <summary>Join row mapping a <see cref="Permission"/> to a <see cref="Role"/> (composite PK).</summary>
public class RolePermission
{
    public Guid RoleId { get; set; }
    public Guid PermissionId { get; set; }

    public Role? Role { get; set; }
    public Permission? Permission { get; set; }
}
