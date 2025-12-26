namespace USP.Core.Models.DTOs.Integrations;

/// <summary>
/// Security alert DTO for external integrations
/// </summary>
public class SecurityAlert
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? User { get; set; }
    public string? IpAddress { get; set; }
    public string? Resource { get; set; }
    public DateTime Timestamp { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
    public string? CorrelationId { get; set; }
    public string Source { get; set; } = "USP";
}

/// <summary>
/// Alert severity levels
/// </summary>
public static class AlertSeverity
{
    public const string Info = "Info";
    public const string Low = "Low";
    public const string Medium = "Medium";
    public const string High = "High";
    public const string Critical = "Critical";
}

/// <summary>
/// Alert types
/// </summary>
public static class AlertType
{
    public const string FailedLogin = "FailedLogin";
    public const string PolicyViolation = "PolicyViolation";
    public const string ThreatDetected = "ThreatDetected";
    public const string SecretRotation = "SecretRotation";
    public const string UnauthorizedAccess = "UnauthorizedAccess";
    public const string SuspiciousActivity = "SuspiciousActivity";
    public const string PAMSessionAlert = "PAMSessionAlert";
    public const string ComplianceViolation = "ComplianceViolation";
}
