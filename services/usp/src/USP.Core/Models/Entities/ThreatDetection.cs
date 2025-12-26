namespace USP.Core.Models.Entities;

/// <summary>
/// Represents a detected security threat
/// </summary>
public class ThreatDetection
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public string ThreatType { get; set; } = string.Empty; // credential_stuffing, brute_force, anomaly, data_exfiltration, etc.
    public string Severity { get; set; } = string.Empty; // low, medium, high, critical
    public int ThreatScore { get; set; } // 0-100
    public double Confidence { get; set; } // 0.0-1.0 ML model confidence
    public string DetectionMethod { get; set; } = string.Empty; // ml_model, rule_based, behavioral
    public string ModelName { get; set; } = string.Empty; // Name of ML model used
    public string ModelVersion { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? Location { get; set; }
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = string.Empty; // detected, investigating, mitigated, false_positive
    public bool AutomaticallyMitigated { get; set; }
    public string? MitigationAction { get; set; } // account_locked, ip_banned, mfa_required, session_terminated
    public DateTime? MitigatedAt { get; set; }
    public Guid? MitigatedBy { get; set; }
    public string? MitigationNotes { get; set; }
    public Guid? SecurityIncidentId { get; set; }
    public string? CorrelationId { get; set; }
    public string? RawData { get; set; } // JSON payload of detection data
    public string? MitreAttackTactics { get; set; } // JSON array of MITRE ATT&CK tactics
    public string? MitreTechniques { get; set; } // JSON array of MITRE ATT&CK techniques
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual ApplicationUser? User { get; set; }
    public virtual ApplicationUser? MitigatedByUser { get; set; }
    public virtual SecurityIncident? SecurityIncident { get; set; }
}
