namespace USP.Core.Models.Entities;

/// <summary>
/// Just-In-Time (JIT) access grant for temporary privilege elevation
/// </summary>
public class JitAccess
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string ResourceType { get; set; } = string.Empty; // Role, Safe, Account, Resource
    public Guid ResourceId { get; set; }
    public string ResourceName { get; set; } = string.Empty;
    public string AccessLevel { get; set; } = string.Empty; // read, checkout, manage, admin
    public string Justification { get; set; } = string.Empty;
    public Guid? TemplateId { get; set; } // Reference to JIT template if used
    public Guid? ApprovalId { get; set; } // Reference to approval if required
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public DateTime? GrantedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public Guid? RevokedBy { get; set; }
    public string? RevocationReason { get; set; }
    public string Status { get; set; } = "pending"; // pending, active, expired, revoked, denied
    public int DurationMinutes { get; set; } = 240; // Default 4 hours
    public bool AutoProvisioningCompleted { get; set; } = false;
    public bool AutoDeprovisioningCompleted { get; set; } = false;
    public string? ProvisioningDetails { get; set; } // JSON: details of what was provisioned
    public string? DeprovisioningDetails { get; set; } // JSON: details of what was deprovisioned
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? Metadata { get; set; } // JSON: additional metadata

    // Navigation properties
    public virtual ApplicationUser User { get; set; } = null!;
    public virtual JitAccessTemplate? Template { get; set; }
    public virtual AccessApproval? Approval { get; set; }
    public virtual ApplicationUser? RevokedByUser { get; set; }
}

/// <summary>
/// Predefined JIT access template for common access patterns
/// </summary>
public class JitAccessTemplate
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty; // Role, Safe, Account, Resource
    public Guid? ResourceId { get; set; } // Specific resource or null for template pattern
    public string AccessLevel { get; set; } = string.Empty;
    public int DefaultDurationMinutes { get; set; } = 240; // 4 hours
    public int MaxDurationMinutes { get; set; } = 480; // 8 hours
    public int MinDurationMinutes { get; set; } = 60; // 1 hour
    public bool RequiresApproval { get; set; } = false;
    public string? ApprovalPolicy { get; set; } // single_approver, dual_control, etc.
    public string? Approvers { get; set; } // JSON: list of approver user IDs
    public bool RequiresJustification { get; set; } = true;
    public string? AllowedRoles { get; set; } // JSON: list of role IDs that can request this
    public bool Active { get; set; } = true;
    public int UsageCount { get; set; } = 0;
    public DateTime? LastUsed { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public string? Metadata { get; set; } // JSON: additional configuration

    // Navigation properties
    public virtual ICollection<JitAccess> AccessGrants { get; set; } = new List<JitAccess>();
}
