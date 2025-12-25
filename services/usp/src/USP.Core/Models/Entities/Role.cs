using Microsoft.AspNetCore.Identity;

namespace USP.Core.Models.Entities;

/// <summary>
/// Role entity for RBAC
/// </summary>
public class Role : IdentityRole<Guid>
{
    public string? Description { get; set; }
    public bool IsBuiltIn { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public virtual ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
