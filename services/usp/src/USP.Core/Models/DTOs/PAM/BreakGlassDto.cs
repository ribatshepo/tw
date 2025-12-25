namespace USP.Core.Models.DTOs.PAM;

/// <summary>
/// Request to activate break-glass emergency access
/// </summary>
public class ActivateBreakGlassRequest
{
    public string Reason { get; set; } = string.Empty;
    public string IncidentType { get; set; } = string.Empty; // security_breach, system_outage, data_loss
    public string Severity { get; set; } = "critical";
    public int DurationMinutes { get; set; } = 60; // Default 1 hour
}

/// <summary>
/// Break-glass access details
/// </summary>
public class BreakGlassAccessDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string UserEmail { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string IncidentType { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public DateTime ActivatedAt { get; set; }
    public DateTime? DeactivatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
    public bool SessionRecordingMandatory { get; set; }
    public Guid? SessionId { get; set; }
    public List<string>? AccessedResources { get; set; }
    public List<string>? ActionsPerformed { get; set; }
    public bool ExecutiveNotified { get; set; }
    public DateTime? ExecutiveNotifiedAt { get; set; }
    public List<string>? NotifiedExecutiveEmails { get; set; }
    public bool RequiresReview { get; set; }
    public Guid? ReviewedBy { get; set; }
    public string? ReviewedByEmail { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNotes { get; set; }
    public string? ReviewDecision { get; set; }
    public string? IpAddress { get; set; }
    public string? Location { get; set; }
    public TimeSpan? RemainingTime { get; set; }
    public TimeSpan? Duration { get; set; }
}

/// <summary>
/// Request to deactivate break-glass access
/// </summary>
public class DeactivateBreakGlassRequest
{
    public List<string>? AccessedResources { get; set; }
    public List<string>? ActionsPerformed { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// Request to review a break-glass access
/// </summary>
public class ReviewBreakGlassRequest
{
    public string ReviewNotes { get; set; } = string.Empty;
    public string ReviewDecision { get; set; } = string.Empty; // justified, unjustified, needs_investigation
}

/// <summary>
/// Break-glass policy configuration
/// </summary>
public class BreakGlassPolicyDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public int DefaultDurationMinutes { get; set; }
    public int MaxDurationMinutes { get; set; }
    public bool RequireJustification { get; set; }
    public int MinJustificationLength { get; set; }
    public bool AutoNotifyExecutives { get; set; }
    public List<string>? ExecutiveEmails { get; set; }
    public bool MandatorySessionRecording { get; set; }
    public bool RequirePostAccessReview { get; set; }
    public int ReviewRequiredWithinHours { get; set; }
    public List<string>? AllowedIncidentTypes { get; set; }
    public List<string>? RestrictedToRoleNames { get; set; }
    public List<string>? NotificationChannels { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// Request to create or update break-glass policy
/// </summary>
public class CreateBreakGlassPolicyRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public int DefaultDurationMinutes { get; set; } = 60;
    public int MaxDurationMinutes { get; set; } = 120;
    public bool RequireJustification { get; set; } = true;
    public int MinJustificationLength { get; set; } = 50;
    public bool AutoNotifyExecutives { get; set; } = true;
    public List<Guid>? ExecutiveUserIds { get; set; }
    public bool MandatorySessionRecording { get; set; } = true;
    public bool RequirePostAccessReview { get; set; } = true;
    public int ReviewRequiredWithinHours { get; set; } = 24;
    public List<string>? AllowedIncidentTypes { get; set; }
    public List<Guid>? RestrictedToRoles { get; set; }
    public List<string>? NotificationChannels { get; set; }
}

/// <summary>
/// Break-glass access statistics
/// </summary>
public class BreakGlassStatisticsDto
{
    public int TotalActivations { get; set; }
    public int ActiveNow { get; set; }
    public int DeactivatedCount { get; set; }
    public int ExpiredCount { get; set; }
    public int PendingReview { get; set; }
    public int ReviewedCount { get; set; }
    public int JustifiedCount { get; set; }
    public int UnjustifiedCount { get; set; }
    public int ActivationsLast24Hours { get; set; }
    public int ActivationsLast7Days { get; set; }
    public int ActivationsLast30Days { get; set; }
    public TimeSpan AverageDuration { get; set; }
    public List<BreakGlassByIncidentTypeDto> ByIncidentType { get; set; } = new();
    public List<BreakGlassBySeverityDto> BySeverity { get; set; } = new();
    public List<BreakGlassByUserDto> TopUsersByActivations { get; set; } = new();
}

/// <summary>
/// Break-glass activations grouped by incident type
/// </summary>
public class BreakGlassByIncidentTypeDto
{
    public string IncidentType { get; set; } = string.Empty;
    public int Count { get; set; }
    public int JustifiedCount { get; set; }
}

/// <summary>
/// Break-glass activations grouped by severity
/// </summary>
public class BreakGlassBySeverityDto
{
    public string Severity { get; set; } = string.Empty;
    public int Count { get; set; }
}

/// <summary>
/// Top users by break-glass activations
/// </summary>
public class BreakGlassByUserDto
{
    public Guid UserId { get; set; }
    public string UserEmail { get; set; } = string.Empty;
    public int ActivationCount { get; set; }
    public int JustifiedCount { get; set; }
    public int UnjustifiedCount { get; set; }
}
