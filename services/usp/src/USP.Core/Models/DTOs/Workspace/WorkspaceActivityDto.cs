namespace USP.Core.Models.DTOs.Workspace;

/// <summary>
/// Workspace activity summary
/// </summary>
public class WorkspaceActivityDto
{
    public Guid WorkspaceId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    // User activity
    public int ActiveUsers { get; set; }
    public int TotalLogins { get; set; }
    public int FailedLogins { get; set; }
    public int MfaChallenges { get; set; }

    // Resource activity
    public int SecretsAccessed { get; set; }
    public int SecretsCreated { get; set; }
    public int SecretsUpdated { get; set; }
    public int SecretsDeleted { get; set; }

    // PAM activity
    public int PamSessionsStarted { get; set; }
    public int AccountCheckouts { get; set; }
    public int PasswordRotations { get; set; }

    // API activity
    public long TotalApiRequests { get; set; }
    public long SuccessfulApiRequests { get; set; }
    public long FailedApiRequests { get; set; }

    // Security events
    public int SecurityAlerts { get; set; }
    public int PolicyViolations { get; set; }
    public int BreakGlassActivations { get; set; }
}
