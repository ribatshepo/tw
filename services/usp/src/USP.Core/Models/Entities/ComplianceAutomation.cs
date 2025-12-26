namespace USP.Core.Models.Entities;

/// <summary>
/// Records automated verification of compliance controls
/// Tracks verification status, scores, and collected evidence
/// </summary>
public class ComplianceControlVerification
{
    public Guid Id { get; set; }

    /// <summary>
    /// Control being verified (FK to ComplianceControl)
    /// </summary>
    public Guid ControlId { get; set; }

    /// <summary>
    /// When verification was performed
    /// </summary>
    public DateTime VerifiedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Verification status: "pass", "fail", "warning", "not_applicable", "manual_review_required"
    /// </summary>
    public string Status { get; set; } = "pending";

    /// <summary>
    /// Compliance score (0-100, where 100 is fully compliant)
    /// </summary>
    public int Score { get; set; }

    /// <summary>
    /// JSON array of evidence collected during verification
    /// Example: [{"type": "audit_log", "count": 1500, "sample": "..."}, {"type": "configuration", "setting": "encryption_enabled", "value": true}]
    /// </summary>
    public string? Evidence { get; set; }

    /// <summary>
    /// Detailed findings from verification
    /// </summary>
    public string? Findings { get; set; }

    /// <summary>
    /// Issues found during verification (JSON array)
    /// </summary>
    public string? Issues { get; set; }

    /// <summary>
    /// Recommendations for remediation
    /// </summary>
    public string? Recommendations { get; set; }

    /// <summary>
    /// Verification method: "automated", "manual", "hybrid"
    /// </summary>
    public string VerificationMethod { get; set; } = "automated";

    /// <summary>
    /// User who performed verification (null for automated)
    /// </summary>
    public Guid? VerifiedBy { get; set; }

    /// <summary>
    /// How long verification took (in seconds)
    /// </summary>
    public int DurationSeconds { get; set; }

    /// <summary>
    /// Next scheduled verification date
    /// </summary>
    public DateTime? NextVerificationDate { get; set; }

    /// <summary>
    /// Verification frequency: "daily", "weekly", "monthly", "quarterly", "annually"
    /// </summary>
    public string? VerificationFrequency { get; set; }

    /// <summary>
    /// JSON metadata for additional context
    /// </summary>
    public string? Metadata { get; set; }

    // Navigation properties
    public virtual ComplianceControl Control { get; set; } = null!;
    public virtual ApplicationUser? Verifier { get; set; }
    public virtual ICollection<ComplianceRemediationTask> RemediationTasks { get; set; } = new List<ComplianceRemediationTask>();
}

/// <summary>
/// Tracks remediation tasks for compliance issues
/// Generated automatically or manually when issues are found
/// </summary>
public class ComplianceRemediationTask
{
    public Guid Id { get; set; }

    /// <summary>
    /// Verification that generated this task (optional)
    /// </summary>
    public Guid? VerificationId { get; set; }

    /// <summary>
    /// Control this task relates to
    /// </summary>
    public Guid ControlId { get; set; }

    /// <summary>
    /// Title of the remediation task
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Detailed description of the issue
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Recommended remediation action
    /// </summary>
    public string RemediationAction { get; set; } = string.Empty;

    /// <summary>
    /// Priority: "critical", "high", "medium", "low"
    /// </summary>
    public string Priority { get; set; } = "medium";

    /// <summary>
    /// Status: "open", "in_progress", "completed", "deferred", "accepted_risk"
    /// </summary>
    public string Status { get; set; } = "open";

    /// <summary>
    /// Assigned to user (optional)
    /// </summary>
    public Guid? AssignedTo { get; set; }

    /// <summary>
    /// Due date for remediation
    /// </summary>
    public DateTime? DueDate { get; set; }

    /// <summary>
    /// When task was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When task was completed
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Completed by user
    /// </summary>
    public Guid? CompletedBy { get; set; }

    /// <summary>
    /// Completion notes
    /// </summary>
    public string? CompletionNotes { get; set; }

    /// <summary>
    /// Impact if not remediated: "critical", "high", "medium", "low"
    /// </summary>
    public string? ImpactLevel { get; set; }

    /// <summary>
    /// Estimated effort: "hours", "days", "weeks"
    /// </summary>
    public string? EstimatedEffort { get; set; }

    /// <summary>
    /// Tags for categorization (JSON array)
    /// </summary>
    public string? Tags { get; set; }

    /// <summary>
    /// JSON metadata
    /// </summary>
    public string? Metadata { get; set; }

    // Navigation properties
    public virtual ComplianceControlVerification? Verification { get; set; }
    public virtual ComplianceControl Control { get; set; } = null!;
    public virtual ApplicationUser? AssignedUser { get; set; }
    public virtual ApplicationUser? CompletedByUser { get; set; }
}

/// <summary>
/// Tracks automated compliance verification schedules
/// Defines when and how often controls should be verified
/// </summary>
public class ComplianceVerificationSchedule
{
    public Guid Id { get; set; }

    /// <summary>
    /// Control to verify
    /// </summary>
    public Guid ControlId { get; set; }

    /// <summary>
    /// Verification frequency: "hourly", "daily", "weekly", "monthly", "quarterly", "annually"
    /// </summary>
    public string Frequency { get; set; } = "daily";

    /// <summary>
    /// Cron expression for custom schedules
    /// Example: "0 4 * * *" (daily at 4 AM)
    /// </summary>
    public string? CronExpression { get; set; }

    /// <summary>
    /// Whether schedule is enabled
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Last time verification ran
    /// </summary>
    public DateTime? LastRunAt { get; set; }

    /// <summary>
    /// Next scheduled run
    /// </summary>
    public DateTime? NextRunAt { get; set; }

    /// <summary>
    /// Last run status: "success", "failure", "partial"
    /// </summary>
    public string? LastRunStatus { get; set; }

    /// <summary>
    /// Last run duration in seconds
    /// </summary>
    public int? LastRunDurationSeconds { get; set; }

    /// <summary>
    /// Notification settings (JSON)
    /// Example: {"email": ["admin@example.com"], "on_failure": true}
    /// </summary>
    public string? NotificationSettings { get; set; }

    /// <summary>
    /// Auto-remediation settings (JSON)
    /// Example: {"enabled": true, "max_severity": "medium"}
    /// </summary>
    public string? AutoRemediationSettings { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual ComplianceControl Control { get; set; } = null!;
}
