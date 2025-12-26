namespace USP.Core.Models.DTOs.Threat;

/// <summary>
/// Security anomaly/threat detection DTO
/// </summary>
public class ThreatAnomalyDto
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public string? UserName { get; set; }
    public string ThreatType { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public int ThreatScore { get; set; }
    public double Confidence { get; set; }
    public string DetectionMethod { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public string ModelVersion { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? Location { get; set; }
    public DateTime DetectedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool AutomaticallyMitigated { get; set; }
    public string? MitigationAction { get; set; }
    public DateTime? MitigatedAt { get; set; }
    public string? MitigationNotes { get; set; }
}

/// <summary>
/// User risk score DTO
/// </summary>
public class UserRiskScoreDto
{
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public int RiskScore { get; set; }
    public string RiskLevel { get; set; } = string.Empty;
    public List<string> RiskFactors { get; set; } = new();
    public int RecentAnomaliesCount { get; set; }
    public DateTime LastCalculatedAt { get; set; }
}

/// <summary>
/// Security alert DTO
/// </summary>
public class SecurityAlertDto
{
    public Guid Id { get; set; }
    public string AlertType { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public Guid? UserId { get; set; }
    public string? UserName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public Guid? AcknowledgedBy { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public Guid? ResolvedBy { get; set; }
}

/// <summary>
/// Request to mark anomaly for investigation
/// </summary>
public class InvestigateAnomalyRequest
{
    public string? Notes { get; set; }
    public bool AssignToSelf { get; set; } = true;
}

/// <summary>
/// Response for list anomalies
/// </summary>
public class AnomalyListResponse
{
    public List<ThreatAnomalyDto> Anomalies { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}

/// <summary>
/// Response for list alerts
/// </summary>
public class AlertListResponse
{
    public List<SecurityAlertDto> Alerts { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}
