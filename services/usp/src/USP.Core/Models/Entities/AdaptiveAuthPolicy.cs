namespace USP.Core.Models.Entities;

/// <summary>
/// Adaptive authentication policy that defines risk-based authentication requirements
/// Determines when step-up authentication is required based on risk assessment
/// </summary>
public class AdaptiveAuthPolicy
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Minimum risk score (0-100) that triggers this policy
    /// </summary>
    public int MinRiskScore { get; set; }

    /// <summary>
    /// Maximum risk score (0-100) for this policy range
    /// </summary>
    public int MaxRiskScore { get; set; }

    /// <summary>
    /// JSON array of required MFA factors: ["totp", "sms", "webauthn", "push"]
    /// </summary>
    public string RequiredFactors { get; set; } = "[]";

    /// <summary>
    /// Number of factors required (1 = single factor, 2+ = multi-factor)
    /// </summary>
    public int RequiredFactorCount { get; set; } = 1;

    /// <summary>
    /// How long the step-up authentication is valid (in minutes)
    /// After this time, user must re-authenticate
    /// </summary>
    public int StepUpValidityMinutes { get; set; } = 15;

    /// <summary>
    /// JSON object defining trigger conditions
    /// Example: {"new_device": true, "new_location": true, "high_value_operation": true}
    /// </summary>
    public string? TriggerConditions { get; set; }

    /// <summary>
    /// JSON array of resource patterns this policy applies to
    /// Example: ["/api/v1/secrets/*", "/api/v1/admin/*"]
    /// </summary>
    public string? ResourcePatterns { get; set; }

    /// <summary>
    /// Whether this policy is currently active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Priority for policy evaluation (higher = evaluated first)
    /// </summary>
    public int Priority { get; set; } = 100;

    /// <summary>
    /// Action to take: "allow", "step_up", "deny", "challenge"
    /// </summary>
    public string Action { get; set; } = "step_up";

    /// <summary>
    /// JSON metadata for additional configuration
    /// </summary>
    public string? Metadata { get; set; }

    public Guid? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual ApplicationUser? Creator { get; set; }
    public virtual ICollection<AuthenticationEvent> AuthenticationEvents { get; set; } = new List<AuthenticationEvent>();
}

/// <summary>
/// Records authentication events for audit and analytics
/// Tracks all authentication attempts including step-up challenges
/// </summary>
public class AuthenticationEvent
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    /// <summary>
    /// Type of authentication event: "login", "step_up", "mfa_challenge", "policy_evaluation"
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// Risk score at time of authentication (0-100)
    /// </summary>
    public int RiskScore { get; set; }

    /// <summary>
    /// Risk level: "low", "medium", "high", "critical"
    /// </summary>
    public string RiskLevel { get; set; } = "low";

    /// <summary>
    /// JSON array of factors used: ["password", "totp", "webauthn"]
    /// </summary>
    public string FactorsUsed { get; set; } = "[]";

    /// <summary>
    /// Outcome: "success", "failure", "denied", "step_up_required"
    /// </summary>
    public string Outcome { get; set; } = string.Empty;

    /// <summary>
    /// Policy that was applied (if any)
    /// </summary>
    public Guid? PolicyId { get; set; }

    /// <summary>
    /// Action taken by policy: "allow", "step_up", "deny"
    /// </summary>
    public string? PolicyAction { get; set; }

    /// <summary>
    /// IP address of authentication attempt
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// User agent string
    /// </summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// Geographic location (city, country)
    /// </summary>
    public string? Location { get; set; }

    /// <summary>
    /// Device fingerprint ID
    /// </summary>
    public string? DeviceFingerprint { get; set; }

    /// <summary>
    /// Whether device is trusted
    /// </summary>
    public bool IsTrustedDevice { get; set; }

    /// <summary>
    /// Resource being accessed (URL path)
    /// </summary>
    public string? ResourcePath { get; set; }

    /// <summary>
    /// Failure reason if outcome is "failure" or "denied"
    /// </summary>
    public string? FailureReason { get; set; }

    /// <summary>
    /// JSON metadata for additional context
    /// </summary>
    public string? Metadata { get; set; }

    public DateTime EventTime { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual ApplicationUser User { get; set; } = null!;
    public virtual AdaptiveAuthPolicy? Policy { get; set; }
}

/// <summary>
/// Tracks active step-up authentication sessions
/// Used to remember that user has recently completed step-up auth
/// </summary>
public class StepUpSession
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    /// <summary>
    /// Session token for step-up authentication
    /// </summary>
    public string SessionToken { get; set; } = string.Empty;

    /// <summary>
    /// Factors successfully completed for step-up
    /// </summary>
    public string CompletedFactors { get; set; } = "[]";

    /// <summary>
    /// When step-up session was initiated
    /// </summary>
    public DateTime InitiatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When step-up session expires
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Resource the step-up grants access to
    /// </summary>
    public string? ResourcePath { get; set; }

    /// <summary>
    /// Whether step-up was successfully completed
    /// </summary>
    public bool IsCompleted { get; set; }

    /// <summary>
    /// When step-up was completed
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// IP address of step-up request
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// Whether session is still valid
    /// </summary>
    public bool IsValid { get; set; } = true;

    /// <summary>
    /// JSON metadata
    /// </summary>
    public string? Metadata { get; set; }

    // Navigation properties
    public virtual ApplicationUser User { get; set; } = null!;
}
