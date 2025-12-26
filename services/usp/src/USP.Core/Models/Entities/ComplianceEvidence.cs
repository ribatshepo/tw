namespace USP.Core.Models.Entities;

/// <summary>
/// Compliance evidence entity for storing proof of control implementation
/// </summary>
public class ComplianceEvidence
{
    public Guid Id { get; set; }
    public Guid ControlId { get; set; }
    public string Framework { get; set; } = string.Empty; // SOC2, HIPAA, PCI-DSS, etc.
    public string EvidenceType { get; set; } = string.Empty; // AuditLog, Configuration, Policy, Screenshot, Document
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? FilePath { get; set; }
    public string? FileHash { get; set; } // SHA-256 hash for integrity
    public string? Metadata { get; set; } // JSON metadata
    public DateTime CollectedAt { get; set; } = DateTime.UtcNow;
    public Guid CollectedBy { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool IsAutomated { get; set; } // Automatically collected vs manually uploaded
    public string Status { get; set; } = "active"; // active, archived, expired

    // Navigation properties
    public virtual ComplianceControl Control { get; set; } = null!;
    public virtual ApplicationUser CollectedByUser { get; set; } = null!;
}

/// <summary>
/// Policy violation entity for tracking compliance violations
/// </summary>
public class PolicyViolation
{
    public Guid Id { get; set; }
    public string ViolationType { get; set; } = string.Empty; // ExcessiveFailedLogins, UnauthorizedAccess, PrivilegeEscalation, etc.
    public string Severity { get; set; } = "medium"; // low, medium, high, critical
    public Guid? UserId { get; set; }
    public string? ResourceType { get; set; }
    public string? ResourceId { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? Details { get; set; } // JSON details
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "open"; // open, acknowledged, resolved, false_positive
    public Guid? AssignedTo { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? Resolution { get; set; }
    public string? IncidentId { get; set; } // Link to security incident if created

    // Navigation properties
    public virtual ApplicationUser? User { get; set; }
    public virtual ApplicationUser? AssignedToUser { get; set; }
}

/// <summary>
/// Compliance dashboard metrics snapshot
/// </summary>
public class ComplianceDashboardSnapshot
{
    public Guid Id { get; set; }
    public DateTime SnapshotDate { get; set; } = DateTime.UtcNow;
    public double OverallScore { get; set; } // 0-100
    public string MetricsJson { get; set; } = string.Empty; // Detailed metrics in JSON
    public int TotalViolations { get; set; }
    public int CriticalViolations { get; set; }
    public int OpenViolations { get; set; }
    public double RiskScore { get; set; } // 0-100
}
