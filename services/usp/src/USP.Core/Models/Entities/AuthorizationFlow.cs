namespace USP.Core.Models.Entities;

/// <summary>
/// Authorization flow definition for multi-step approval workflows
/// </summary>
public class AuthorizationFlow
{
    public Guid Id { get; set; }
    public string FlowName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public int RequiredApprovals { get; set; } = 1;
    public string ApproverRoles { get; set; } = string.Empty; // JSON array of role names
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ICollection<AuthorizationFlowInstance> FlowInstances { get; set; } = new List<AuthorizationFlowInstance>();
}

/// <summary>
/// Instance of an authorization flow for a specific request
/// </summary>
public class AuthorizationFlowInstance
{
    public Guid Id { get; set; }
    public Guid FlowId { get; set; }
    public Guid RequesterId { get; set; }
    public string ResourceType { get; set; } = string.Empty;
    public string ResourceId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Status { get; set; } = "pending"; // pending, approved, denied, expired
    public string Context { get; set; } = "{}"; // JSON context
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }

    // Navigation properties
    public AuthorizationFlow Flow { get; set; } = null!;
    public ApplicationUser Requester { get; set; } = null!;
    public ICollection<FlowApproval> Approvals { get; set; } = new List<FlowApproval>();
}

/// <summary>
/// Individual approval in an authorization flow instance
/// </summary>
public class FlowApproval
{
    public Guid Id { get; set; }
    public Guid FlowInstanceId { get; set; }
    public Guid ApproverId { get; set; }
    public string Status { get; set; } = "pending"; // pending, approved, denied
    public string Comment { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ApprovedAt { get; set; }

    // Navigation properties
    public AuthorizationFlowInstance FlowInstance { get; set; } = null!;
    public ApplicationUser Approver { get; set; } = null!;
}
