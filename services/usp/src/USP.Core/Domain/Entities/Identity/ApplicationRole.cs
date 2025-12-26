using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace USP.Core.Domain.Entities.Identity;

/// <summary>
/// Represents a role in the USP system with permission management
/// </summary>
public class ApplicationRole : IdentityRole
{
    /// <summary>
    /// Description of the role's purpose and responsibilities
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Indicates whether this is a system-defined role (cannot be deleted)
    /// </summary>
    public bool IsSystemRole { get; set; }

    /// <summary>
    /// Priority level for role precedence (higher = takes precedence in conflicts)
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    /// Metadata stored as JSON (custom attributes, configurations, etc.)
    /// </summary>
    public string? Metadata { get; set; }

    /// <summary>
    /// Timestamp when the role was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Timestamp when the role was last updated
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Soft delete timestamp (null if not deleted)
    /// </summary>
    public DateTime? DeletedAt { get; set; }

    // Navigation properties

    /// <summary>
    /// User-role assignments for this role
    /// </summary>
    public virtual ICollection<IdentityUserRole<string>> UserRoles { get; set; } = new List<IdentityUserRole<string>>();

    /// <summary>
    /// Permissions granted to this role
    /// </summary>
    public virtual ICollection<Security.Permission> Permissions { get; set; } = new List<Security.Permission>();
}
