namespace USP.Core.Models.DTOs.Authentication;

/// <summary>
/// Result of policy evaluation
/// Indicates what action should be taken based on risk assessment
/// </summary>
public class PolicyEvaluationResultDto
{
    public string Action { get; set; } = "allow"; // "allow", "step_up", "deny"
    public int RiskScore { get; set; }
    public string RiskLevel { get; set; } = "low"; // "low", "medium", "high", "critical"
    public Guid? PolicyId { get; set; }
    public string? PolicyName { get; set; }
    public List<string> RequiredFactors { get; set; } = new();
    public int RequiredFactorCount { get; set; }
    public int StepUpValidityMinutes { get; set; }
    public string? Reason { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
}

/// <summary>
/// Step-up authentication challenge details
/// </summary>
public class StepUpChallengeDto
{
    public string SessionToken { get; set; } = string.Empty;
    public List<string> RequiredFactors { get; set; } = new();
    public int RequiredFactorCount { get; set; }
    public DateTime ExpiresAt { get; set; }
    public int ValidityMinutes { get; set; }
    public string? ResourcePath { get; set; }
    public Dictionary<string, object>? ChallengeData { get; set; } // Factor-specific challenge data
}

/// <summary>
/// Result of step-up factor validation
/// </summary>
public class StepUpValidationResultDto
{
    public bool IsValid { get; set; }
    public string Factor { get; set; } = string.Empty;
    public List<string> CompletedFactors { get; set; } = new();
    public List<string> RemainingFactors { get; set; } = new();
    public bool AllFactorsCompleted { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Result of step-up session completion
/// </summary>
public class StepUpCompletionResultDto
{
    public bool IsCompleted { get; set; }
    public string SessionToken { get; set; } = string.Empty;
    public List<string> CompletedFactors { get; set; } = new();
    public DateTime ExpiresAt { get; set; }
    public string? ResourcePath { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Authentication event for audit trail
/// </summary>
public class AuthenticationEventDto
{
    public Guid EventId { get; set; }
    public Guid UserId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public int RiskScore { get; set; }
    public string RiskLevel { get; set; } = string.Empty;
    public List<string> FactorsUsed { get; set; } = new();
    public string Outcome { get; set; } = string.Empty;
    public Guid? PolicyId { get; set; }
    public string? PolicyName { get; set; }
    public string? PolicyAction { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? Location { get; set; }
    public string? DeviceFingerprint { get; set; }
    public bool IsTrustedDevice { get; set; }
    public string? ResourcePath { get; set; }
    public string? FailureReason { get; set; }
    public DateTime EventTime { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
}

/// <summary>
/// Adaptive authentication policy configuration
/// </summary>
public class AdaptiveAuthPolicyDto
{
    public Guid PolicyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int MinRiskScore { get; set; }
    public int MaxRiskScore { get; set; }
    public List<string> RequiredFactors { get; set; } = new();
    public int RequiredFactorCount { get; set; }
    public int StepUpValidityMinutes { get; set; }
    public Dictionary<string, bool>? TriggerConditions { get; set; }
    public List<string>? ResourcePatterns { get; set; }
    public bool IsActive { get; set; }
    public int Priority { get; set; }
    public string Action { get; set; } = "step_up";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Request to create or update adaptive auth policy
/// </summary>
public class CreateAdaptiveAuthPolicyDto
{
    public Guid? PolicyId { get; set; } // Null for create, set for update
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int MinRiskScore { get; set; } = 0;
    public int MaxRiskScore { get; set; } = 100;
    public List<string> RequiredFactors { get; set; } = new();
    public int RequiredFactorCount { get; set; } = 1;
    public int StepUpValidityMinutes { get; set; } = 15;
    public Dictionary<string, bool>? TriggerConditions { get; set; }
    public List<string>? ResourcePatterns { get; set; }
    public bool IsActive { get; set; } = true;
    public int Priority { get; set; } = 100;
    public string Action { get; set; } = "step_up";
}

/// <summary>
/// Request to initiate step-up authentication
/// </summary>
public class InitiateStepUpDto
{
    public string? ResourcePath { get; set; }
}

/// <summary>
/// Request to validate step-up factor
/// </summary>
public class ValidateStepUpFactorDto
{
    public string SessionToken { get; set; } = string.Empty;
    public string Factor { get; set; } = string.Empty; // "totp", "sms", "webauthn", "push"
    public string Credential { get; set; } = string.Empty; // Factor-specific credential
}

/// <summary>
/// Request to complete step-up session
/// </summary>
public class CompleteStepUpDto
{
    public string SessionToken { get; set; } = string.Empty;
}

/// <summary>
/// Authentication statistics for a user
/// </summary>
public class AuthenticationStatisticsDto
{
    public Guid UserId { get; set; }
    public int TotalAuthenticationEvents { get; set; }
    public int SuccessfulLogins { get; set; }
    public int FailedLogins { get; set; }
    public int StepUpChallenges { get; set; }
    public int StepUpSuccesses { get; set; }
    public int StepUpFailures { get; set; }
    public int PolicyDenials { get; set; }
    public double AverageRiskScore { get; set; }
    public int HighRiskEvents { get; set; } // Risk > 70
    public int TrustedDeviceLogins { get; set; }
    public int NewDeviceLogins { get; set; }
    public List<string> MostUsedFactors { get; set; } = new();
    public Dictionary<string, int> EventTypeBreakdown { get; set; } = new();
    public Dictionary<string, int> OutcomeBreakdown { get; set; } = new();
    public DateTime? LastAuthenticationEvent { get; set; }
    public DateTime? LastStepUpChallenge { get; set; }
}
