namespace USP.Core.Models.Entities;

/// <summary>
/// Security incident entity for tracking and managing security events
/// </summary>
public class SecurityIncident
{
    public Guid Id { get; set; }
    public string IncidentNumber { get; set; } = string.Empty; // INC-YYYYMMDD-0001
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty; // low, medium, high, critical
    public string Priority { get; set; } = string.Empty; // p1, p2, p3, p4
    public string Status { get; set; } = string.Empty; // detected, investigating, contained, eradicated, recovered, closed
    public string Category { get; set; } = string.Empty; // unauthorized_access, malware, data_breach, dos, insider_threat
    public Guid? ReportedBy { get; set; }
    public Guid? AssignedTo { get; set; }
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
    public DateTime? AcknowledgedAt { get; set; }
    public DateTime? ContainedAt { get; set; }
    public DateTime? EradicatedAt { get; set; }
    public DateTime? RecoveredAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public string? RootCause { get; set; }
    public string? ImpactAssessment { get; set; }
    public string? RemediationSteps { get; set; }
    public string? LessonsLearned { get; set; }
    public bool DataBreachOccurred { get; set; }
    public int? RecordsAffected { get; set; }
    public bool RegulatoryNotificationRequired { get; set; }
    public bool ExecutiveNotified { get; set; }
    public DateTime? ExecutiveNotifiedAt { get; set; }
    public string? AffectedSystems { get; set; } // JSON array
    public string? AffectedUsers { get; set; } // JSON array of user IDs
    public string? MitreAttackTactics { get; set; } // JSON array
    public string? MitreTechniques { get; set; } // JSON array
    public string? TicketingSystemId { get; set; } // Jira, ServiceNow ticket ID
    public string? TicketingSystemUrl { get; set; }
    public string? Timeline { get; set; } // JSON array of timeline events
    public string? Metadata { get; set; } // JSON for additional data
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual ApplicationUser? Reporter { get; set; }
    public virtual ApplicationUser? Assignee { get; set; }
    public virtual ICollection<ThreatDetection> ThreatDetections { get; set; } = new List<ThreatDetection>();
}
