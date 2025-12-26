namespace USP.Core.Services.Authorization;

/// <summary>
/// Context-aware access control evaluator
/// Evaluates access based on time, location, device, and risk factors
/// </summary>
public interface IContextEvaluator
{
    /// <summary>
    /// Evaluate context-based access decision
    /// </summary>
    Task<ContextEvaluationResponse> EvaluateContextAsync(ContextEvaluationRequest request);

    /// <summary>
    /// Check if access is allowed at current time
    /// </summary>
    Task<bool> IsTimeBasedAccessAllowedAsync(Guid userId, string resource, DateTime? requestTime = null);

    /// <summary>
    /// Check if access is allowed from location
    /// </summary>
    Task<bool> IsLocationBasedAccessAllowedAsync(Guid userId, string resource, string location);

    /// <summary>
    /// Check if device meets compliance requirements
    /// </summary>
    Task<bool> IsDeviceCompliantAsync(Guid userId, string deviceId);

    /// <summary>
    /// Calculate access risk score
    /// </summary>
    Task<int> CalculateAccessRiskScoreAsync(ContextEvaluationRequest request);

    /// <summary>
    /// Get context policy for resource
    /// </summary>
    Task<ContextPolicy?> GetContextPolicyAsync(string resourceType);
}

/// <summary>
/// Context evaluation request
/// </summary>
public class ContextEvaluationRequest
{
    public Guid UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public string? ResourceId { get; set; }

    // Time context
    public DateTime? RequestTime { get; set; }

    // Location context
    public string? IpAddress { get; set; }
    public string? GeoLocation { get; set; }
    public string? NetworkZone { get; set; }

    // Device context
    public string? DeviceId { get; set; }
    public string? DeviceType { get; set; }
    public bool? DeviceCompliant { get; set; }
    public string? UserAgent { get; set; }

    // Risk context
    public int? UserRiskScore { get; set; }
    public bool? ImpossibleTravel { get; set; }
}

/// <summary>
/// Context evaluation response
/// </summary>
public class ContextEvaluationResponse
{
    public bool Allowed { get; set; }
    public string Decision { get; set; } = "deny";
    public List<string> Reasons { get; set; } = new();
    public int RiskScore { get; set; }
    public string RiskLevel { get; set; } = "low"; // low, medium, high, critical
    public Dictionary<string, bool> ContextChecks { get; set; } = new();
    public string? RequiredAction { get; set; } // null, "mfa", "approval", "deny"
}

/// <summary>
/// Context-based access policy
/// </summary>
public class ContextPolicy
{
    public Guid Id { get; set; }
    public string ResourceType { get; set; } = string.Empty;
    public string Action { get; set; } = "*";

    // Time restrictions
    public bool EnableTimeRestriction { get; set; }
    public string? AllowedDaysOfWeek { get; set; } // "Monday,Tuesday,Wednesday,Thursday,Friday"
    public TimeSpan? AllowedStartTime { get; set; }
    public TimeSpan? AllowedEndTime { get; set; }

    // Location restrictions
    public bool EnableLocationRestriction { get; set; }
    public string[]? AllowedCountries { get; set; }
    public string[]? DeniedCountries { get; set; }
    public string[]? AllowedNetworkZones { get; set; }

    // Device restrictions
    public bool EnableDeviceRestriction { get; set; }
    public bool RequireCompliantDevice { get; set; }
    public string[]? AllowedDeviceTypes { get; set; }

    // Risk restrictions
    public bool EnableRiskRestriction { get; set; }
    public int? MaxAllowedRiskScore { get; set; }
    public bool DenyImpossibleTravel { get; set; }

    // Adaptive requirements
    public bool RequireMfaOnHighRisk { get; set; }
    public bool RequireApprovalOnHighRisk { get; set; }
    public int? HighRiskThreshold { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Request to create context policy
/// </summary>
public class CreateContextPolicyRequest
{
    public string ResourceType { get; set; } = string.Empty;
    public string Action { get; set; } = "*";
    public bool EnableTimeRestriction { get; set; }
    public string? AllowedDaysOfWeek { get; set; }
    public TimeSpan? AllowedStartTime { get; set; }
    public TimeSpan? AllowedEndTime { get; set; }
    public bool EnableLocationRestriction { get; set; }
    public List<string>? AllowedCountries { get; set; }
    public List<string>? DeniedCountries { get; set; }
    public List<string>? AllowedNetworkZones { get; set; }
    public bool EnableDeviceRestriction { get; set; }
    public bool RequireCompliantDevice { get; set; }
    public List<string>? AllowedDeviceTypes { get; set; }
    public bool EnableRiskRestriction { get; set; }
    public int? MaxAllowedRiskScore { get; set; }
    public bool DenyImpossibleTravel { get; set; }
    public bool RequireMfaOnHighRisk { get; set; }
    public bool RequireApprovalOnHighRisk { get; set; }
    public int? HighRiskThreshold { get; set; }
}
