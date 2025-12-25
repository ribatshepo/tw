namespace USP.Core.Models.Entities;

/// <summary>
/// Compliance report entity
/// </summary>
public class ComplianceReport
{
    public Guid Id { get; set; }
    public string Framework { get; set; } = string.Empty; // SOC2, HIPAA, PCI-DSS, ISO27001, NIST800-53, GDPR
    public string ReportType { get; set; } = string.Empty; // Full, Gap Analysis, Control Assessment
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public Guid GeneratedBy { get; set; }
    public string Status { get; set; } = "completed"; // generating, completed, failed
    public string ReportPath { get; set; } = string.Empty;
    public string Format { get; set; } = "PDF"; // PDF, JSON, CSV
    public int TotalControls { get; set; }
    public int ImplementedControls { get; set; }
    public int PartialControls { get; set; }
    public int NotImplementedControls { get; set; }
    public double ComplianceScore { get; set; } // 0-100%
    public string? Summary { get; set; }
    public string? Recommendations { get; set; }

    // Navigation properties
    public virtual ApplicationUser GeneratedByUser { get; set; } = null!;
    public virtual ICollection<ComplianceControl> Controls { get; set; } = new List<ComplianceControl>();
}

/// <summary>
/// Compliance control assessment
/// </summary>
public class ComplianceControl
{
    public Guid Id { get; set; }
    public Guid ReportId { get; set; }
    public string ControlId { get; set; } = string.Empty; // e.g., SOC2-CC6.1, HIPAA-164.308
    public string ControlName { get; set; } = string.Empty;
    public string ControlDescription { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty; // Access Control, Encryption, Monitoring, etc.
    public string Status { get; set; } = "not_implemented"; // implemented, partial, not_implemented, not_applicable
    public string? Implementation { get; set; }
    public string? Evidence { get; set; }
    public string? Gaps { get; set; }
    public DateTime? LastAssessed { get; set; }
    public Guid? AssessedBy { get; set; }

    // Navigation properties
    public virtual ComplianceReport Report { get; set; } = null!;
    public virtual ApplicationUser? AssessedByUser { get; set; }
}
