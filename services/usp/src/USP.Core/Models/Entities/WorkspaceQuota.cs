namespace USP.Core.Models.Entities;

/// <summary>
/// Resource quotas for a workspace
/// Enforces limits based on subscription tier
/// </summary>
public class WorkspaceQuota
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }

    /// <summary>
    /// Maximum number of users allowed in this workspace
    /// </summary>
    public int MaxUsers { get; set; } = 5;

    /// <summary>
    /// Maximum number of secrets allowed
    /// </summary>
    public int MaxSecrets { get; set; } = 100;

    /// <summary>
    /// Maximum number of API keys allowed
    /// </summary>
    public int MaxApiKeys { get; set; } = 50;

    /// <summary>
    /// Maximum number of safes/vaults allowed
    /// </summary>
    public int MaxSafes { get; set; } = 10;

    /// <summary>
    /// Maximum number of privileged accounts
    /// </summary>
    public int MaxPrivilegedAccounts { get; set; } = 10;

    /// <summary>
    /// Maximum concurrent PAM sessions
    /// </summary>
    public int MaxPamSessions { get; set; } = 5;

    /// <summary>
    /// Maximum API requests per hour
    /// </summary>
    public int MaxApiRequestsPerHour { get; set; } = 1000;

    /// <summary>
    /// Maximum storage in MB for secrets and recordings
    /// </summary>
    public long MaxStorageMb { get; set; } = 1024;

    /// <summary>
    /// Maximum number of workspaces (for parent workspaces)
    /// </summary>
    public int MaxChildWorkspaces { get; set; } = 0;

    /// <summary>
    /// Maximum audit log retention in days
    /// </summary>
    public int AuditRetentionDays { get; set; } = 90;

    /// <summary>
    /// Whether session recording is enabled
    /// </summary>
    public bool SessionRecordingEnabled { get; set; } = true;

    /// <summary>
    /// Whether advanced compliance features are enabled
    /// </summary>
    public bool AdvancedComplianceEnabled { get; set; } = false;

    /// <summary>
    /// Whether custom authentication methods are allowed
    /// </summary>
    public bool CustomAuthMethodsEnabled { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual Workspace Workspace { get; set; } = null!;
}
