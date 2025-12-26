namespace USP.Core.Models.DTOs.Workspace;

/// <summary>
/// Security alert for a workspace
/// </summary>
public class WorkspaceSecurityAlertDto
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }

    /// <summary>
    /// Alert severity: low, medium, high, critical
    /// </summary>
    public string Severity { get; set; } = "low";

    /// <summary>
    /// Alert type: suspicious_login, quota_exceeded, policy_violation, etc.
    /// </summary>
    public string AlertType { get; set; } = string.Empty;

    /// <summary>
    /// Alert title
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Alert description
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Affected user
    /// </summary>
    public Guid? UserId { get; set; }
    public string? UserEmail { get; set; }

    /// <summary>
    /// Affected resource
    /// </summary>
    public string? ResourceType { get; set; }
    public string? ResourceId { get; set; }

    /// <summary>
    /// Alert status: open, acknowledged, resolved, false_positive
    /// </summary>
    public string Status { get; set; } = "open";

    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
}
