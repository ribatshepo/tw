namespace USP.Core.Models.Entities;

/// <summary>
/// Workspace entity for multi-tenancy support
/// Provides complete data isolation at database level
/// </summary>
public class Workspace
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid OwnerId { get; set; }

    /// <summary>
    /// Parent workspace ID for hierarchical workspaces
    /// </summary>
    public Guid? ParentWorkspaceId { get; set; }

    /// <summary>
    /// Workspace status: active, suspended, deleted
    /// </summary>
    public string Status { get; set; } = "active";

    /// <summary>
    /// Workspace settings as JSON
    /// </summary>
    public string? Settings { get; set; }

    /// <summary>
    /// Metadata for extensibility
    /// </summary>
    public string? Metadata { get; set; }

    /// <summary>
    /// Subscription tier: free, pro, enterprise
    /// </summary>
    public string SubscriptionTier { get; set; } = "free";

    /// <summary>
    /// Custom subdomain for the workspace (e.g., acme.usp.example.com)
    /// </summary>
    public string? CustomDomain { get; set; }

    /// <summary>
    /// Workspace-specific authentication requirements
    /// </summary>
    public bool RequireMfa { get; set; } = false;

    /// <summary>
    /// Minimum password length for this workspace
    /// </summary>
    public int MinPasswordLength { get; set; } = 8;

    /// <summary>
    /// IP whitelist for this workspace (JSON array)
    /// </summary>
    public string? IpWhitelist { get; set; }

    /// <summary>
    /// Session timeout in minutes
    /// </summary>
    public int SessionTimeoutMinutes { get; set; } = 480;

    /// <summary>
    /// Whether this workspace is billable
    /// </summary>
    public bool IsBillable { get; set; } = false;

    /// <summary>
    /// Monthly cost in cents
    /// </summary>
    public long MonthlyCostCents { get; set; } = 0;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }

    // Navigation properties
    public virtual ApplicationUser Owner { get; set; } = null!;
    public virtual Workspace? ParentWorkspace { get; set; }
    public virtual ICollection<Workspace> ChildWorkspaces { get; set; } = new List<Workspace>();
    public virtual ICollection<WorkspaceMember> Members { get; set; } = new List<WorkspaceMember>();
    public virtual WorkspaceQuota? Quota { get; set; }
    public virtual WorkspaceUsage? Usage { get; set; }
}
