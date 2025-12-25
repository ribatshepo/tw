namespace USP.Core.Models.DTOs.Authorization;

/// <summary>
/// Request to evaluate ABAC policy
/// </summary>
public class AbacEvaluationRequest
{
    public Guid SubjectId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public string? ResourceId { get; set; }
    public Dictionary<string, object>? Context { get; set; }
    public Dictionary<string, object>? SubjectAttributes { get; set; }
    public Dictionary<string, object>? ResourceAttributes { get; set; }
    public Dictionary<string, object>? EnvironmentAttributes { get; set; }
}

/// <summary>
/// Response from ABAC policy evaluation
/// </summary>
public class AbacEvaluationResponse
{
    public bool Allowed { get; set; }
    public string Decision { get; set; } = string.Empty; // "allow", "deny", "not_applicable"
    public List<string> Reasons { get; set; } = new();
    public List<string> AppliedPolicies { get; set; } = new();
    public Dictionary<string, object> EvaluationContext { get; set; } = new();
    public TimeSpan EvaluationTime { get; set; }
}

/// <summary>
/// Request to check authorization using HCL policy
/// </summary>
public class HclAuthorizationRequest
{
    public Guid UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Resource { get; set; } = string.Empty;
    public Dictionary<string, object>? Context { get; set; }
}

/// <summary>
/// Response from HCL authorization check
/// </summary>
public class HclAuthorizationResponse
{
    public bool Authorized { get; set; }
    public string PolicyName { get; set; } = string.Empty;
    public List<string> MatchedRules { get; set; } = new();
    public string DenyReason { get; set; } = string.Empty;
}

/// <summary>
/// Request to create or update ABAC policy
/// </summary>
public class CreatePolicyRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string PolicyType { get; set; } = "ABAC"; // ABAC, HCL, RBAC
    public string Policy { get; set; } = string.Empty; // HCL or JSON
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Policy DTO for API responses
/// </summary>
public class PolicyDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string PolicyType { get; set; } = string.Empty;
    public string Policy { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid CreatedBy { get; set; }
}

/// <summary>
/// Request to simulate policy evaluation
/// </summary>
public class PolicySimulationRequest
{
    public Guid PolicyId { get; set; }
    public Guid UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Resource { get; set; } = string.Empty;
    public Dictionary<string, object>? Context { get; set; }
    public Dictionary<string, object>? SubjectAttributes { get; set; }
    public Dictionary<string, object>? ResourceAttributes { get; set; }
}

/// <summary>
/// Response from policy simulation
/// </summary>
public class PolicySimulationResponse
{
    public bool Allowed { get; set; }
    public string Decision { get; set; } = string.Empty;
    public List<string> EvaluationSteps { get; set; } = new();
    public List<string> AppliedRules { get; set; } = new();
    public Dictionary<string, object> AttributesUsed { get; set; } = new();
    public string Explanation { get; set; } = string.Empty;
}

/// <summary>
/// Attribute extraction request
/// </summary>
public class AttributeExtractionRequest
{
    public Guid UserId { get; set; }
    public string ResourceType { get; set; } = string.Empty;
    public string? ResourceId { get; set; }
    public Dictionary<string, object>? AdditionalContext { get; set; }
}

/// <summary>
/// Extracted attributes for ABAC evaluation
/// </summary>
public class ExtractedAttributes
{
    public Dictionary<string, object> SubjectAttributes { get; set; } = new();
    public Dictionary<string, object> ResourceAttributes { get; set; } = new();
    public Dictionary<string, object> EnvironmentAttributes { get; set; } = new();
    public Dictionary<string, object> ContextAttributes { get; set; } = new();
}

/// <summary>
/// Flow-based authorization request
/// </summary>
public class AuthorizationFlowRequest
{
    public string FlowName { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Resource { get; set; } = string.Empty;
    public Dictionary<string, object>? Context { get; set; }
    public List<string>? RequiredApprovals { get; set; }
}

/// <summary>
/// Flow-based authorization response
/// </summary>
public class AuthorizationFlowResponse
{
    public bool Authorized { get; set; }
    public string FlowStatus { get; set; } = string.Empty; // "approved", "pending", "denied"
    public List<string> PendingApprovers { get; set; } = new();
    public List<ApprovalStep> ApprovalSteps { get; set; } = new();
    public string DenyReason { get; set; } = string.Empty;
}

/// <summary>
/// Approval step in authorization flow
/// </summary>
public class ApprovalStep
{
    public Guid Id { get; set; }
    public string ApproverRole { get; set; } = string.Empty;
    public Guid? ApproverId { get; set; }
    public string Status { get; set; } = string.Empty; // "pending", "approved", "denied"
    public DateTime? ApprovedAt { get; set; }
    public string? Comment { get; set; }
}

/// <summary>
/// Column-level access control request
/// </summary>
public class ColumnAccessRequest
{
    public Guid UserId { get; set; }
    public string TableName { get; set; } = string.Empty;
    public List<string> RequestedColumns { get; set; } = new();
    public string Operation { get; set; } = string.Empty; // "read", "write"
}

/// <summary>
/// Column-level access control response
/// </summary>
public class ColumnAccessResponse
{
    public List<string> AllowedColumns { get; set; } = new();
    public List<string> DeniedColumns { get; set; } = new();
    public Dictionary<string, string> ColumnRestrictions { get; set; } = new();
}
