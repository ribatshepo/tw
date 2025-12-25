namespace USP.Core.Models.DTOs.Compliance;

/// <summary>
/// Request to generate compliance report
/// </summary>
public class GenerateComplianceReportRequest
{
    public string Framework { get; set; } = string.Empty; // SOC2, HIPAA, PCI-DSS, ISO27001, NIST800-53, GDPR
    public string ReportType { get; set; } = "Full"; // Full, GapAnalysis, ControlAssessment
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public string Format { get; set; } = "PDF"; // PDF, JSON, CSV
    public bool IncludeEvidence { get; set; } = true;
    public bool IncludeRecommendations { get; set; } = true;
}

/// <summary>
/// Compliance report DTO
/// </summary>
public class ComplianceReportDto
{
    public Guid Id { get; set; }
    public string Framework { get; set; } = string.Empty;
    public string ReportType { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public string GeneratedBy { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
    public int TotalControls { get; set; }
    public int ImplementedControls { get; set; }
    public int PartialControls { get; set; }
    public int NotImplementedControls { get; set; }
    public double ComplianceScore { get; set; }
    public string? Summary { get; set; }
    public string? Recommendations { get; set; }
    public string? DownloadUrl { get; set; }
}

/// <summary>
/// Compliance status summary
/// </summary>
public class ComplianceStatusDto
{
    public string Framework { get; set; } = string.Empty;
    public double ComplianceScore { get; set; }
    public int TotalControls { get; set; }
    public int ImplementedControls { get; set; }
    public int PartialControls { get; set; }
    public int NotImplementedControls { get; set; }
    public DateTime? LastAssessed { get; set; }
    public List<string> CriticalGaps { get; set; } = new();
}

/// <summary>
/// Control assessment DTO
/// </summary>
public class ControlAssessmentDto
{
    public string ControlId { get; set; } = string.Empty;
    public string ControlName { get; set; } = string.Empty;
    public string ControlDescription { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Implementation { get; set; }
    public string? Evidence { get; set; }
    public string? Gaps { get; set; }
    public DateTime? LastAssessed { get; set; }
}
