using USP.Core.Domain.Enums;

namespace USP.Core.Interfaces.Services.Authorization;

/// <summary>
/// Provides authorization operations including policy evaluation, permission checking, and access control.
/// Supports RBAC (Role-Based), ABAC (Attribute-Based), and HCL (HashiCorp Configuration Language) policies.
/// </summary>
public interface IAuthorizationService
{
    /// <summary>
    /// Checks if a user is authorized to perform an action on a resource.
    /// Evaluates all applicable policies (RBAC, ABAC, HCL) in priority order.
    /// </summary>
    /// <param name="userId">The user ID making the request</param>
    /// <param name="resource">The resource being accessed (e.g., "secrets/production/db-password")</param>
    /// <param name="action">The action being performed (e.g., "read", "write", "delete")</param>
    /// <param name="context">Optional context attributes (IP address, time, device info, etc.)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Authorization result indicating whether access is granted</returns>
    Task<AuthorizationResult> CheckAuthorizationAsync(
        string userId,
        string resource,
        string action,
        AuthorizationContext? context = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks authorization for multiple resource-action pairs in a single batch request.
    /// More efficient than multiple individual authorization checks.
    /// </summary>
    /// <param name="userId">The user ID making the request</param>
    /// <param name="requests">Collection of resource-action pairs to check</param>
    /// <param name="context">Optional context attributes</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of authorization results</returns>
    Task<IEnumerable<AuthorizationResult>> CheckBatchAuthorizationAsync(
        string userId,
        IEnumerable<ResourceActionPair> requests,
        AuthorizationContext? context = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a user has a specific permission.
    /// Permission format: "resource:action" (e.g., "secrets:write", "users:delete")
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="permission">The permission string in format "resource:action"</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if user has the permission, false otherwise</returns>
    Task<bool> HasPermissionAsync(
        string userId,
        string permission,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all effective permissions for a user (from all assigned roles).
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of permission strings</returns>
    Task<IEnumerable<string>> GetUserPermissionsAsync(
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Simulates policy evaluation without actually granting access.
    /// Useful for testing policies before deploying them.
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="resource">The resource being accessed</param>
    /// <param name="action">The action being performed</param>
    /// <param name="context">Optional context attributes</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Detailed evaluation result including which policies matched</returns>
    Task<PolicyEvaluationResult> SimulatePolicyEvaluationAsync(
        string userId,
        string resource,
        string action,
        AuthorizationContext? context = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Evaluates a specific RBAC policy for a user.
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="resource">The resource being accessed</param>
    /// <param name="action">The action being performed</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if user has access via RBAC, false otherwise</returns>
    Task<bool> EvaluateRBACAsync(
        string userId,
        string resource,
        string action,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Evaluates ABAC (Attribute-Based Access Control) policies for a user.
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="resource">The resource being accessed</param>
    /// <param name="action">The action being performed</param>
    /// <param name="context">Context attributes for ABAC evaluation</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>ABAC evaluation result</returns>
    Task<ABACEvaluationResult> EvaluateABACAsync(
        string userId,
        string resource,
        string action,
        AuthorizationContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Evaluates HCL (HashiCorp Configuration Language) policies.
    /// Compatible with Vault policy syntax.
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="path">The resource path (Vault-style)</param>
    /// <param name="operation">The operation being performed</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if user has access via HCL policy, false otherwise</returns>
    Task<bool> EvaluateHCLAsync(
        string userId,
        string path,
        string operation,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all active policies applicable to a user.
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="policyType">Optional filter by policy type</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of applicable policies</returns>
    Task<IEnumerable<PolicySummary>> GetApplicablePoliciesAsync(
        string userId,
        PolicyType? policyType = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of an authorization check.
/// </summary>
public class AuthorizationResult
{
    /// <summary>
    /// Indicates whether access is granted.
    /// </summary>
    public required bool IsAuthorized { get; set; }

    /// <summary>
    /// The resource that was checked.
    /// </summary>
    public required string Resource { get; set; }

    /// <summary>
    /// The action that was checked.
    /// </summary>
    public required string Action { get; set; }

    /// <summary>
    /// Reason for the decision (for auditing and debugging).
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// ID of the policy that made the final decision.
    /// </summary>
    public string? PolicyId { get; set; }

    /// <summary>
    /// Type of policy that made the decision (RBAC, ABAC, or HCL).
    /// </summary>
    public PolicyType? PolicyType { get; set; }

    /// <summary>
    /// Timestamp when the authorization check was performed.
    /// </summary>
    public DateTime EvaluatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Context information for authorization decisions.
/// </summary>
public class AuthorizationContext
{
    /// <summary>
    /// Client IP address.
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// Client user agent.
    /// </summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// Device fingerprint.
    /// </summary>
    public string? DeviceFingerprint { get; set; }

    /// <summary>
    /// Time of the request.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Geographic location (country, region, city).
    /// </summary>
    public string? Location { get; set; }

    /// <summary>
    /// Additional custom attributes for ABAC evaluation.
    /// </summary>
    public Dictionary<string, object> Attributes { get; set; } = new();

    /// <summary>
    /// Resource attributes (classification, sensitivity level, owner, etc.).
    /// </summary>
    public Dictionary<string, object> ResourceAttributes { get; set; } = new();

    /// <summary>
    /// Subject attributes (department, clearance level, roles, etc.).
    /// </summary>
    public Dictionary<string, object> SubjectAttributes { get; set; } = new();
}

/// <summary>
/// Represents a resource-action pair for batch authorization checks.
/// </summary>
public class ResourceActionPair
{
    public required string Resource { get; set; }
    public required string Action { get; set; }
}

/// <summary>
/// Detailed result of policy evaluation (for simulation/debugging).
/// </summary>
public class PolicyEvaluationResult
{
    /// <summary>
    /// Final authorization decision.
    /// </summary>
    public required bool IsAuthorized { get; set; }

    /// <summary>
    /// Policies that were evaluated, in order.
    /// </summary>
    public List<EvaluatedPolicy> EvaluatedPolicies { get; set; } = new();

    /// <summary>
    /// User's roles at the time of evaluation.
    /// </summary>
    public List<string> UserRoles { get; set; } = new();

    /// <summary>
    /// User's effective permissions at the time of evaluation.
    /// </summary>
    public List<string> UserPermissions { get; set; } = new();

    /// <summary>
    /// Detailed explanation of the decision.
    /// </summary>
    public string? Explanation { get; set; }
}

/// <summary>
/// Information about a single policy that was evaluated.
/// </summary>
public class EvaluatedPolicy
{
    public required string PolicyId { get; set; }
    public required string PolicyName { get; set; }
    public required PolicyType PolicyType { get; set; }
    public required bool Matched { get; set; }
    public required string Effect { get; set; } // "allow" or "deny"
    public int Priority { get; set; }
    public string? MatchReason { get; set; }
}

/// <summary>
/// Result of ABAC policy evaluation.
/// </summary>
public class ABACEvaluationResult
{
    public required bool IsAuthorized { get; set; }
    public List<string> MatchedPolicies { get; set; } = new();
    public List<string> FailedConditions { get; set; } = new();
    public string? Reason { get; set; }
}

/// <summary>
/// Summary of a policy.
/// </summary>
public class PolicySummary
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public required PolicyType Type { get; set; }
    public required string Effect { get; set; }
    public int Priority { get; set; }
    public bool IsActive { get; set; }
    public string? Description { get; set; }
}
