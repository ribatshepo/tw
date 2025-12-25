using USP.Core.Models.DTOs.Authorization;

namespace USP.Core.Services.Authorization;

/// <summary>
/// HCL (HashiCorp Configuration Language) policy evaluator interface
/// </summary>
public interface IHclPolicyEvaluator
{
    /// <summary>
    /// Parse HCL policy string into structured format
    /// </summary>
    HclPolicy ParsePolicy(string hclText);

    /// <summary>
    /// Evaluate HCL policy for authorization decision
    /// </summary>
    Task<HclAuthorizationResponse> EvaluateAsync(HclAuthorizationRequest request);

    /// <summary>
    /// Validate HCL policy syntax
    /// </summary>
    (bool IsValid, List<string> Errors) ValidatePolicy(string hclText);

    /// <summary>
    /// Check if user has specific capability on a path
    /// </summary>
    Task<bool> HasCapabilityAsync(Guid userId, string path, string capability);

    /// <summary>
    /// Get all capabilities for user on a path
    /// </summary>
    Task<List<string>> GetCapabilitiesAsync(Guid userId, string path);
}

/// <summary>
/// Parsed HCL policy structure
/// </summary>
public class HclPolicy
{
    public string Name { get; set; } = string.Empty;
    public List<HclPathPolicy> PathPolicies { get; set; } = new();
}

/// <summary>
/// HCL path-based policy
/// </summary>
public class HclPathPolicy
{
    public string Path { get; set; } = string.Empty;
    public List<string> Capabilities { get; set; } = new();
    public Dictionary<string, object>? Conditions { get; set; }
    public Dictionary<string, List<string>>? AllowedParameters { get; set; }
    public List<string>? DeniedParameters { get; set; }
    public List<string>? RequiredParameters { get; set; }
    public int? MinWrappingTtl { get; set; }
    public int? MaxWrappingTtl { get; set; }
}
