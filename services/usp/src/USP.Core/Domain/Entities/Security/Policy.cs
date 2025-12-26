using System.ComponentModel.DataAnnotations;
using USP.Core.Domain.Enums;

namespace USP.Core.Domain.Entities.Security;

/// <summary>
/// Represents an access control policy (RBAC, ABAC, or HCL)
/// </summary>
public class Policy
{
    /// <summary>
    /// Unique identifier for the policy
    /// </summary>
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Unique name for the policy
    /// </summary>
    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = null!;

    /// <summary>
    /// Description of the policy's purpose
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Type of policy (RBAC, ABAC, HCL)
    /// </summary>
    [Required]
    public PolicyType Type { get; set; }

    /// <summary>
    /// Policy content (JSON for ABAC, HCL for Vault-compatible, null for RBAC)
    /// </summary>
    public string? Content { get; set; }

    /// <summary>
    /// Effect of the policy (allow or deny)
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string Effect { get; set; } = "allow";

    /// <summary>
    /// Priority for policy evaluation (higher = evaluated first)
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    /// Indicates whether this policy is currently active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Indicates whether this is a system-defined policy (cannot be deleted)
    /// </summary>
    public bool IsSystemPolicy { get; set; }

    /// <summary>
    /// Version number for policy updates
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// Metadata stored as JSON (tags, labels, etc.)
    /// </summary>
    public string? Metadata { get; set; }

    /// <summary>
    /// Timestamp when the policy was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// User ID who created the policy
    /// </summary>
    public string? CreatedBy { get; set; }

    /// <summary>
    /// Timestamp when the policy was last updated
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// User ID who last updated the policy
    /// </summary>
    public string? UpdatedBy { get; set; }

    /// <summary>
    /// Soft delete timestamp (null if not deleted)
    /// </summary>
    public DateTime? DeletedAt { get; set; }
}
