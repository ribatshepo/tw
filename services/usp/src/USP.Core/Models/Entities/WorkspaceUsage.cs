namespace USP.Core.Models.Entities;

/// <summary>
/// Tracks current resource usage for a workspace
/// Updated in real-time to enforce quotas
/// </summary>
public class WorkspaceUsage
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }

    /// <summary>
    /// Current number of users in the workspace
    /// </summary>
    public int CurrentUsers { get; set; } = 0;

    /// <summary>
    /// Current number of secrets
    /// </summary>
    public int CurrentSecrets { get; set; } = 0;

    /// <summary>
    /// Current number of privileged accounts
    /// </summary>
    public int CurrentPrivilegedAccounts { get; set; } = 0;

    /// <summary>
    /// Current number of active PAM sessions
    /// </summary>
    public int CurrentPamSessions { get; set; } = 0;

    /// <summary>
    /// API requests made in the current hour
    /// </summary>
    public int ApiRequestsThisHour { get; set; } = 0;

    /// <summary>
    /// When the API request counter was last reset
    /// </summary>
    public DateTime ApiRequestsResetAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Current storage usage in MB
    /// </summary>
    public long CurrentStorageMb { get; set; } = 0;

    /// <summary>
    /// Number of child workspaces
    /// </summary>
    public int CurrentChildWorkspaces { get; set; } = 0;

    /// <summary>
    /// Total API requests (all time)
    /// </summary>
    public long TotalApiRequests { get; set; } = 0;

    /// <summary>
    /// Total audit logs generated
    /// </summary>
    public long TotalAuditLogs { get; set; } = 0;

    /// <summary>
    /// Total session recordings
    /// </summary>
    public long TotalSessionRecordings { get; set; } = 0;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual Workspace Workspace { get; set; } = null!;
}
