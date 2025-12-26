namespace USP.Core.Models.DTOs.Integrations;

/// <summary>
/// Audit event for external integrations (SIEM, Kafka)
/// </summary>
public class AuditEvent
{
    public Guid EventId { get; set; }
    public DateTime Timestamp { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public Guid? UserId { get; set; }
    public string? Username { get; set; }
    public string ResourceType { get; set; } = string.Empty;
    public string? ResourceId { get; set; }
    public string IpAddress { get; set; } = string.Empty;
    public string? UserAgent { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
    public string? CorrelationId { get; set; }
    public string Severity { get; set; } = "Info";
    public string Source { get; set; } = "USP";
}

/// <summary>
/// Event types for audit events
/// </summary>
public static class EventType
{
    public const string UserCreated = "user.created";
    public const string UserDeleted = "user.deleted";
    public const string UserUpdated = "user.updated";
    public const string SecretCreated = "secret.created";
    public const string SecretAccessed = "secret.accessed";
    public const string SecretRotated = "secret.rotated";
    public const string SecretDeleted = "secret.deleted";
    public const string SessionStarted = "session.started";
    public const string SessionEnded = "session.ended";
    public const string PolicyViolated = "policy.violated";
    public const string ThreatDetected = "threat.detected";
    public const string LoginAttempt = "auth.login";
    public const string LogoutAttempt = "auth.logout";
    public const string MfaEnrolled = "mfa.enrolled";
    public const string MfaVerified = "mfa.verified";
}
