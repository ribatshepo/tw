using System.ComponentModel.DataAnnotations;

namespace USP.Core.Domain.Entities.Security;

/// <summary>
/// Represents an Attribute-Based Access Control (ABAC) policy
/// Stored in database table: access_policies
/// </summary>
public class AccessPolicy
{
    /// <summary>
    /// Unique identifier for the access policy
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
    /// Description of the policy
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Policy effect (allow or deny)
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string Effect { get; set; } = "allow";

    /// <summary>
    /// Subject attributes (JSON) - who the policy applies to
    /// e.g., {"role": "data-engineer", "department": "analytics", "clearance_level": 3}
    /// </summary>
    [Required]
    public string Subjects { get; set; } = "{}";

    /// <summary>
    /// Resource attributes (JSON) - what resources the policy applies to
    /// e.g., {"classification": "confidential", "sensitivity_level": 2, "owner": "analytics-team"}
    /// </summary>
    [Required]
    public string Resources { get; set; } = "{}";

    /// <summary>
    /// Actions allowed (array) - what operations can be performed
    /// e.g., ["read", "list"]
    /// </summary>
    [Required]
    public string Actions { get; set; } = "[]";

    /// <summary>
    /// Conditions (JSON) - environmental/contextual constraints
    /// e.g., {"time_of_day": "business_hours", "ip_address": "10.0.0.0/8", "device_compliance": true}
    /// </summary>
    public string? Conditions { get; set; }

    /// <summary>
    /// Priority for policy evaluation (higher = evaluated first)
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    /// Indicates whether this policy is currently active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Timestamp when the policy was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Timestamp when the policy was last updated
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Soft delete timestamp (null if not deleted)
    /// </summary>
    public DateTime? DeletedAt { get; set; }
}
