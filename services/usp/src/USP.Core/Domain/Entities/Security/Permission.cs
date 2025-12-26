using System.ComponentModel.DataAnnotations;

namespace USP.Core.Domain.Entities.Security;

/// <summary>
/// Represents a granular permission in the RBAC system
/// Format: resource:action (e.g., "secrets:write", "users:delete")
/// </summary>
public class Permission
{
    /// <summary>
    /// Unique identifier for the permission
    /// </summary>
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Resource being protected (e.g., "secrets", "users", "roles", "audit")
    /// </summary>
    [Required]
    [MaxLength(255)]
    public string Resource { get; set; } = null!;

    /// <summary>
    /// Action allowed on the resource (e.g., "create", "read", "update", "delete", "list", "manage")
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string Action { get; set; } = null!;

    /// <summary>
    /// Full permission string (computed: resource:action)
    /// </summary>
    public string FullPermission => $"{Resource}:{Action}";

    /// <summary>
    /// Human-readable description of what this permission allows
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Indicates whether this is a system-defined permission (cannot be deleted)
    /// </summary>
    public bool IsSystemPermission { get; set; }

    /// <summary>
    /// Timestamp when the permission was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Timestamp when the permission was last updated
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Soft delete timestamp (null if not deleted)
    /// </summary>
    public DateTime? DeletedAt { get; set; }

    // Navigation properties

    /// <summary>
    /// Roles that have been granted this permission
    /// </summary>
    public virtual ICollection<Identity.ApplicationRole> Roles { get; set; } = new List<Identity.ApplicationRole>();
}
