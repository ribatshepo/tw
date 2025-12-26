namespace USP.Core.Models.Entities;

/// <summary>
/// Automated threat response action log
/// </summary>
public class ThreatResponseAction
{
    public Guid Id { get; set; }
    public Guid ThreatDetectionId { get; set; }
    public Guid? SecurityIncidentId { get; set; }
    public string ActionType { get; set; } = string.Empty; // account_lock, ip_ban, session_terminate, mfa_enforce, notification
    public string ActionDetails { get; set; } = string.Empty;
    public string PlaybookName { get; set; } = string.Empty;
    public string PlaybookVersion { get; set; } = string.Empty;
    public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;
    public Guid? ExecutedBy { get; set; } // Null if automated
    public bool Automated { get; set; } = true;
    public string Status { get; set; } = string.Empty; // pending, executing, completed, failed, rolled_back
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; }
    public DateTime? RolledBackAt { get; set; }
    public string? RollbackReason { get; set; }
    public string? TargetUserId { get; set; }
    public string? TargetIpAddress { get; set; }
    public string? TargetSessionId { get; set; }
    public int? BanDurationMinutes { get; set; }
    public string? NotificationChannels { get; set; } // JSON array: email, slack, pagerduty
    public string? NotificationRecipients { get; set; } // JSON array
    public string? AuditTrail { get; set; } // JSON array of action steps
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual ThreatDetection ThreatDetection { get; set; } = null!;
    public virtual SecurityIncident? SecurityIncident { get; set; }
    public virtual ApplicationUser? Executor { get; set; }
}
