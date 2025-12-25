namespace USP.Core.Models.DTOs.Webhook;

/// <summary>
/// Request to create a webhook
/// </summary>
public class CreateWebhookRequest
{
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<string> Events { get; set; } = new();
    public string AuthenticationType { get; set; } = "secret"; // secret, oauth2, mtls, none
    public string? SecretToken { get; set; }
    public string? OAuth2ClientId { get; set; }
    public string? OAuth2ClientSecret { get; set; }
    public string? OAuth2TokenUrl { get; set; }
    public Dictionary<string, string>? CustomHeaders { get; set; }
    public string? PayloadTemplate { get; set; }
    public int MaxRetries { get; set; } = 5;
    public int TimeoutSeconds { get; set; } = 30;
    public bool VerifySsl { get; set; } = true;
    public int CircuitBreakerThreshold { get; set; } = 5;
    public int CircuitBreakerResetMinutes { get; set; } = 5;
}

/// <summary>
/// Request to update a webhook
/// </summary>
public class UpdateWebhookRequest
{
    public string? Name { get; set; }
    public string? Url { get; set; }
    public string? Description { get; set; }
    public List<string>? Events { get; set; }
    public bool? Active { get; set; }
    public string? AuthenticationType { get; set; }
    public string? SecretToken { get; set; }
    public Dictionary<string, string>? CustomHeaders { get; set; }
    public string? PayloadTemplate { get; set; }
    public int? MaxRetries { get; set; }
    public int? TimeoutSeconds { get; set; }
    public bool? VerifySsl { get; set; }
}

/// <summary>
/// Webhook DTO
/// </summary>
public class WebhookDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<string> Events { get; set; } = new();
    public bool Active { get; set; }
    public string AuthenticationType { get; set; } = string.Empty;
    public bool HasSecretToken { get; set; }
    public Dictionary<string, string>? CustomHeaders { get; set; }
    public int MaxRetries { get; set; }
    public int TimeoutSeconds { get; set; }
    public bool VerifySsl { get; set; }
    public string CircuitBreakerState { get; set; } = string.Empty;
    public int ConsecutiveFailures { get; set; }
    public int TotalDeliveries { get; set; }
    public int SuccessfulDeliveries { get; set; }
    public int FailedDeliveries { get; set; }
    public DateTime? LastTriggeredAt { get; set; }
    public DateTime? LastSuccessAt { get; set; }
    public DateTime? LastFailureAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// Webhook delivery DTO
/// </summary>
public class WebhookDeliveryDto
{
    public Guid Id { get; set; }
    public Guid WebhookId { get; set; }
    public string WebhookName { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int AttemptCount { get; set; }
    public int ResponseStatus { get; set; }
    public string? ResponseBody { get; set; }
    public string? ErrorMessage { get; set; }
    public int DurationMs { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? NextRetryAt { get; set; }
}

/// <summary>
/// Request to test a webhook
/// </summary>
public class TestWebhookRequest
{
    public string? EventType { get; set; } = "webhook.test";
    public Dictionary<string, object>? TestPayload { get; set; }
}

/// <summary>
/// Webhook delivery filter request
/// </summary>
public class WebhookDeliveryFilterRequest
{
    public Guid? WebhookId { get; set; }
    public string? EventType { get; set; }
    public string? Status { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

/// <summary>
/// Available webhook events
/// </summary>
public static class WebhookEvents
{
    // User events
    public const string UserCreated = "user.created";
    public const string UserUpdated = "user.updated";
    public const string UserDeleted = "user.deleted";
    public const string UserLoggedIn = "user.logged_in";
    public const string UserLoggedOut = "user.logged_out";

    // Secret events
    public const string SecretWritten = "secret.written";
    public const string SecretRead = "secret.read";
    public const string SecretDeleted = "secret.deleted";
    public const string SecretRotated = "secret.rotated";

    // Session events
    public const string SessionCreated = "session.created";
    public const string SessionRevoked = "session.revoked";
    public const string SessionExpired = "session.expired";

    // MFA events
    public const string MfaEnabled = "mfa.enabled";
    public const string MfaDisabled = "mfa.disabled";
    public const string MfaChallengeFailed = "mfa.challenge_failed";

    // Role events
    public const string RoleAssigned = "role.assigned";
    public const string RoleRevoked = "role.revoked";

    // API Key events
    public const string ApiKeyCreated = "apikey.created";
    public const string ApiKeyRevoked = "apikey.revoked";
    public const string ApiKeyRotated = "apikey.rotated";

    // Security events
    public const string SecurityBreachDetected = "security.breach_detected";
    public const string SecurityPolicyViolation = "security.policy_violation";
    public const string UnauthorizedAccessAttempt = "security.unauthorized_access";

    // Audit events
    public const string AuditLogExported = "audit.log_exported";
    public const string AuditChainIntegrityFailed = "audit.chain_integrity_failed";

    // Compliance events
    public const string ComplianceReportGenerated = "compliance.report_generated";
    public const string ComplianceGapDetected = "compliance.gap_detected";

    public static List<string> GetAll()
    {
        return new List<string>
        {
            UserCreated, UserUpdated, UserDeleted, UserLoggedIn, UserLoggedOut,
            SecretWritten, SecretRead, SecretDeleted, SecretRotated,
            SessionCreated, SessionRevoked, SessionExpired,
            MfaEnabled, MfaDisabled, MfaChallengeFailed,
            RoleAssigned, RoleRevoked,
            ApiKeyCreated, ApiKeyRevoked, ApiKeyRotated,
            SecurityBreachDetected, SecurityPolicyViolation, UnauthorizedAccessAttempt,
            AuditLogExported, AuditChainIntegrityFailed,
            ComplianceReportGenerated, ComplianceGapDetected
        };
    }
}
