namespace USP.Core.Models.Entities;

/// <summary>
/// Break-glass emergency access for critical incident response
/// </summary>
public class BreakGlassAccess
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Reason { get; set; } = string.Empty; // Detailed emergency justification
    public string IncidentType { get; set; } = string.Empty; // security_breach, system_outage, data_loss, etc.
    public string Severity { get; set; } = "critical"; // critical, high
    public DateTime ActivatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeactivatedAt { get; set; }
    public DateTime ExpiresAt { get; set; } // Auto-deactivation time (1-2 hours)
    public string Status { get; set; } = "active"; // active, deactivated, expired, under_review, reviewed
    public int DurationMinutes { get; set; } = 60; // Default 1 hour
    public bool SessionRecordingMandatory { get; set; } = true;
    public Guid? SessionId { get; set; } // Link to PrivilegedSession if applicable
    public string? AccessedResources { get; set; } // JSON: list of resources accessed
    public string? ActionsPerformed { get; set; } // JSON: list of actions taken
    public bool ExecutiveNotified { get; set; } = false;
    public DateTime? ExecutiveNotifiedAt { get; set; }
    public string? NotifiedExecutives { get; set; } // JSON: list of executive user IDs notified
    public bool RequiresReview { get; set; } = true;
    public Guid? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNotes { get; set; }
    public string? ReviewDecision { get; set; } // justified, unjustified, needs_investigation
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? Location { get; set; } // Geographic location
    public string? DeviceFingerprint { get; set; }
    public string? Metadata { get; set; } // JSON: additional emergency context

    // Navigation properties
    public virtual ApplicationUser User { get; set; } = null!;
    public virtual ApplicationUser? Reviewer { get; set; }
    public virtual PrivilegedSession? Session { get; set; }
}

/// <summary>
/// Configuration for break-glass access policies
/// </summary>
public class BreakGlassPolicy
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public int DefaultDurationMinutes { get; set; } = 60;
    public int MaxDurationMinutes { get; set; } = 120;
    public bool RequireJustification { get; set; } = true;
    public int MinJustificationLength { get; set; } = 50;
    public bool AutoNotifyExecutives { get; set; } = true;
    public string? ExecutiveUserIds { get; set; } // JSON: list of executive user IDs
    public bool MandatorySessionRecording { get; set; } = true;
    public bool RequirePostAccessReview { get; set; } = true;
    public int ReviewRequiredWithinHours { get; set; } = 24;
    public string? AllowedIncidentTypes { get; set; } // JSON: list of allowed incident types
    public string? RestrictedToRoles { get; set; } // JSON: list of role IDs allowed to use break-glass
    public string? NotificationChannels { get; set; } // JSON: email, sms, slack, etc.
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public string? Metadata { get; set; } // JSON: additional policy configuration
}
