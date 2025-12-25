using USP.Core.Models.DTOs.Authorization;

namespace USP.Core.Services.Authorization;

/// <summary>
/// ABAC (Attribute-Based Access Control) engine interface
/// </summary>
public interface IAbacEngine
{
    /// <summary>
    /// Evaluate ABAC policy for authorization decision
    /// </summary>
    Task<AbacEvaluationResponse> EvaluateAsync(AbacEvaluationRequest request);

    /// <summary>
    /// Extract attributes from subject, resource, and environment
    /// </summary>
    Task<ExtractedAttributes> ExtractAttributesAsync(AttributeExtractionRequest request);

    /// <summary>
    /// Check if user has access based on ABAC policies
    /// </summary>
    Task<bool> HasAccessAsync(Guid userId, string action, string resourceType, string? resourceId = null, Dictionary<string, object>? context = null);

    /// <summary>
    /// Get all applicable policies for a given context
    /// </summary>
    Task<List<PolicyDto>> GetApplicablePoliciesAsync(Guid userId, string action, string resourceType);

    /// <summary>
    /// Simulate policy evaluation for testing
    /// </summary>
    Task<PolicySimulationResponse> SimulatePolicyAsync(PolicySimulationRequest request);
}
