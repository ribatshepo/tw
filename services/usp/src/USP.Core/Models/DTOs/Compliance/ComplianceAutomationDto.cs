namespace USP.Core.Models.DTOs.Compliance;

/// <summary>
/// Result of automated control verification
/// </summary>
public class ControlVerificationResultDto
{
    public Guid VerificationId { get; set; }
    public Guid ControlId { get; set; }
    public string ControlName { get; set; } = string.Empty;
    public string ControlDescription { get; set; } = string.Empty;
    public DateTime VerifiedAt { get; set; }
    public string Status { get; set; } = string.Empty; // "pass", "fail", "warning", "manual_review_required"
    public int Score { get; set; } // 0-100
    public List<EvidenceItemDto> Evidence { get; set; } = new();
    public string? Findings { get; set; }
    public List<string> Issues { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
    public string VerificationMethod { get; set; } = "automated";
    public Guid? VerifiedBy { get; set; }
    public string? VerifierName { get; set; }
    public int DurationSeconds { get; set; }
    public DateTime? NextVerificationDate { get; set; }
}

/// <summary>
/// Evidence collected for a control
/// </summary>
public class ControlEvidenceDto
{
    public Guid ControlId { get; set; }
    public DateTime CollectedAt { get; set; }
    public List<EvidenceItemDto> Items { get; set; } = new();
    public int TotalItems { get; set; }
    public Dictionary<string, int> EvidenceTypeCounts { get; set; } = new();
}

/// <summary>
/// Individual evidence item
/// </summary>
public class EvidenceItemDto
{
    public string Type { get; set; } = string.Empty; // "audit_log", "configuration", "policy", "screenshot", "document"
    public string Description { get; set; } = string.Empty;
    public object? Value { get; set; }
    public DateTime? Timestamp { get; set; }
    public string? Source { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Automated compliance report
/// </summary>
public class AutomatedComplianceReportDto
{
    public Guid ReportId { get; set; }
    public string FrameworkName { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public string Format { get; set; } = "json";
    public int TotalControls { get; set; }
    public int PassedControls { get; set; }
    public int FailedControls { get; set; }
    public int WarningControls { get; set; }
    public double ComplianceScore { get; set; } // 0-100
    public List<ControlVerificationResultDto> ControlResults { get; set; } = new();
    public List<RemediationTaskDto> OpenRemediationTasks { get; set; } = new();
    public Dictionary<string, int> StatusBreakdown { get; set; } = new();
    public string? ReportContent { get; set; } // HTML, PDF content, or JSON string
    public string? ReportUrl { get; set; } // If stored in blob storage
}

/// <summary>
/// Summary of continuous compliance check run
/// </summary>
public class ComplianceCheckSummaryDto
{
    public DateTime RunStartedAt { get; set; }
    public DateTime RunCompletedAt { get; set; }
    public int DurationSeconds { get; set; }
    public int TotalControlsVerified { get; set; }
    public int PassedControls { get; set; }
    public int FailedControls { get; set; }
    public int WarningControls { get; set; }
    public int RemediationTasksCreated { get; set; }
    public List<string> FrameworksVerified { get; set; } = new();
    public Dictionary<string, int> FrameworkScores { get; set; } = new();
    public List<ControlVerificationResultDto> FailedControlDetails { get; set; } = new();
}

/// <summary>
/// Remediation task for compliance issue
/// </summary>
public class RemediationTaskDto
{
    public Guid TaskId { get; set; }
    public Guid? VerificationId { get; set; }
    public Guid ControlId { get; set; }
    public string ControlName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string RemediationAction { get; set; } = string.Empty;
    public string Priority { get; set; } = "medium";
    public string Status { get; set; } = "open";
    public Guid? AssignedTo { get; set; }
    public string? AssignedToName { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public Guid? CompletedBy { get; set; }
    public string? CompletedByName { get; set; }
    public string? CompletionNotes { get; set; }
    public string? ImpactLevel { get; set; }
    public string? EstimatedEffort { get; set; }
    public List<string> Tags { get; set; } = new();
}

/// <summary>
/// Request to create remediation task
/// </summary>
public class CreateRemediationTaskDto
{
    public Guid? VerificationId { get; set; }
    public Guid ControlId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string RemediationAction { get; set; } = string.Empty;
    public string Priority { get; set; } = "medium";
    public Guid? AssignedTo { get; set; }
    public DateTime? DueDate { get; set; }
    public string? ImpactLevel { get; set; }
    public string? EstimatedEffort { get; set; }
    public List<string>? Tags { get; set; }
}

/// <summary>
/// Compliance dashboard summary
/// </summary>
public class ComplianceDashboardDto
{
    public string? FrameworkName { get; set; }
    public DateTime GeneratedAt { get; set; }
    public double OverallComplianceScore { get; set; }
    public int TotalControls { get; set; }
    public int PassedControls { get; set; }
    public int FailedControls { get; set; }
    public int WarningControls { get; set; }
    public int NotVerifiedControls { get; set; }
    public int OpenRemediationTasks { get; set; }
    public int CriticalRemediationTasks { get; set; }
    public int OverdueRemediationTasks { get; set; }
    public DateTime? LastVerificationRun { get; set; }
    public DateTime? NextScheduledVerification { get; set; }
    public Dictionary<string, double> FrameworkScores { get; set; } = new();
    public Dictionary<string, int> ControlStatusBreakdown { get; set; } = new();
    public List<ControlVerificationResultDto> RecentFailures { get; set; } = new();
    public List<RemediationTaskDto> CriticalTasks { get; set; } = new();
}

/// <summary>
/// Verification schedule configuration
/// </summary>
public class VerificationScheduleDto
{
    public Guid ScheduleId { get; set; }
    public Guid ControlId { get; set; }
    public string ControlName { get; set; } = string.Empty;
    public string Frequency { get; set; } = "daily";
    public string? CronExpression { get; set; }
    public bool IsEnabled { get; set; }
    public DateTime? LastRunAt { get; set; }
    public DateTime? NextRunAt { get; set; }
    public string? LastRunStatus { get; set; }
    public int? LastRunDurationSeconds { get; set; }
    public Dictionary<string, object>? NotificationSettings { get; set; }
    public Dictionary<string, object>? AutoRemediationSettings { get; set; }
}

/// <summary>
/// Request to create/update verification schedule
/// </summary>
public class CreateVerificationScheduleDto
{
    public Guid ControlId { get; set; }
    public string Frequency { get; set; } = "daily";
    public string? CronExpression { get; set; }
    public bool IsEnabled { get; set; } = true;
    public Dictionary<string, object>? NotificationSettings { get; set; }
    public Dictionary<string, object>? AutoRemediationSettings { get; set; }
}

/// <summary>
/// Compliance trend data over time
/// </summary>
public class ComplianceTrendDto
{
    public string FrameworkName { get; set; } = string.Empty;
    public int Days { get; set; }
    public List<ComplianceTrendDataPointDto> DataPoints { get; set; } = new();
    public double AverageScore { get; set; }
    public double HighestScore { get; set; }
    public double LowestScore { get; set; }
    public string Trend { get; set; } = "stable"; // "improving", "declining", "stable"
}

/// <summary>
/// Single data point in compliance trend
/// </summary>
public class ComplianceTrendDataPointDto
{
    public DateTime Date { get; set; }
    public double Score { get; set; }
    public int PassedControls { get; set; }
    public int FailedControls { get; set; }
    public int TotalControls { get; set; }
}

/// <summary>
/// Request to verify a control
/// </summary>
public class VerifyControlRequestDto
{
    public Guid ControlId { get; set; }
}

/// <summary>
/// Request to generate compliance report
/// </summary>
public class GenerateReportRequestDto
{
    public string FrameworkName { get; set; } = string.Empty;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string Format { get; set; } = "json"; // "json", "pdf", "html", "csv"
}
