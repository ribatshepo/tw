namespace USP.Core.Models.DTOs.Workspace;

/// <summary>
/// User activity within a workspace
/// </summary>
public class UserActivityDto
{
    public Guid UserId { get; set; }
    public string UserEmail { get; set; } = string.Empty;
    public string? UserFullName { get; set; }
    public string Role { get; set; } = "member";

    public int LoginCount { get; set; }
    public DateTime? LastLogin { get; set; }
    public int SecretsAccessed { get; set; }
    public int PamSessionsStarted { get; set; }
    public long ApiRequestsMade { get; set; }
    public int AuditLogsGenerated { get; set; }

    public DateTime? LastActivityAt { get; set; }
}
