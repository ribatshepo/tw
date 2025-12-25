namespace USP.Core.Models.DTOs.PAM;

/// <summary>
/// Request to create a JIT access grant
/// </summary>
public class RequestJitAccessRequest
{
    public string ResourceType { get; set; } = string.Empty; // Role, Safe, Account, Resource
    public Guid ResourceId { get; set; }
    public string AccessLevel { get; set; } = string.Empty; // read, checkout, manage, admin
    public string Justification { get; set; } = string.Empty;
    public int DurationMinutes { get; set; } = 240; // Default 4 hours
    public Guid? TemplateId { get; set; } // Optional: use predefined template
}

/// <summary>
/// JIT access grant details
/// </summary>
public class JitAccessDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string UserEmail { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public Guid ResourceId { get; set; }
    public string ResourceName { get; set; } = string.Empty;
    public string AccessLevel { get; set; } = string.Empty;
    public string Justification { get; set; } = string.Empty;
    public Guid? TemplateId { get; set; }
    public string? TemplateName { get; set; }
    public Guid? ApprovalId { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? GrantedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public Guid? RevokedBy { get; set; }
    public string? RevokedByEmail { get; set; }
    public string? RevocationReason { get; set; }
    public string Status { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
    public bool AutoProvisioningCompleted { get; set; }
    public bool AutoDeprovisioningCompleted { get; set; }
    public TimeSpan? RemainingTime { get; set; }
}

/// <summary>
/// Request to create a JIT access template
/// </summary>
public class CreateJitTemplateRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public Guid? ResourceId { get; set; }
    public string AccessLevel { get; set; } = string.Empty;
    public int DefaultDurationMinutes { get; set; } = 240;
    public int MaxDurationMinutes { get; set; } = 480;
    public int MinDurationMinutes { get; set; } = 60;
    public bool RequiresApproval { get; set; } = false;
    public string? ApprovalPolicy { get; set; }
    public List<Guid>? Approvers { get; set; }
    public bool RequiresJustification { get; set; } = true;
    public List<Guid>? AllowedRoles { get; set; }
}

/// <summary>
/// JIT access template details
/// </summary>
public class JitAccessTemplateDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public Guid? ResourceId { get; set; }
    public string AccessLevel { get; set; } = string.Empty;
    public int DefaultDurationMinutes { get; set; }
    public int MaxDurationMinutes { get; set; }
    public int MinDurationMinutes { get; set; }
    public bool RequiresApproval { get; set; }
    public string? ApprovalPolicy { get; set; }
    public List<string>? ApproverEmails { get; set; }
    public bool RequiresJustification { get; set; }
    public List<string>? AllowedRoleNames { get; set; }
    public bool Active { get; set; }
    public int UsageCount { get; set; }
    public DateTime? LastUsed { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Request to revoke a JIT access grant
/// </summary>
public class RevokeJitAccessRequest
{
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// JIT access statistics
/// </summary>
public class JitAccessStatisticsDto
{
    public int TotalRequests { get; set; }
    public int ActiveGrants { get; set; }
    public int PendingApprovals { get; set; }
    public int ExpiredGrants { get; set; }
    public int RevokedGrants { get; set; }
    public int DeniedRequests { get; set; }
    public int GrantsLast24Hours { get; set; }
    public int GrantsLast7Days { get; set; }
    public int GrantsLast30Days { get; set; }
    public TimeSpan AverageGrantDuration { get; set; }
    public List<JitAccessByResourceTypeDto> AccessByResourceType { get; set; } = new();
    public List<JitAccessByUserDto> TopUsersByRequests { get; set; } = new();
    public List<JitAccessTemplateUsageDto> TemplateUsage { get; set; } = new();
}

/// <summary>
/// JIT access grouped by resource type
/// </summary>
public class JitAccessByResourceTypeDto
{
    public string ResourceType { get; set; } = string.Empty;
    public int Count { get; set; }
    public int ActiveCount { get; set; }
}

/// <summary>
/// Top users by JIT access requests
/// </summary>
public class JitAccessByUserDto
{
    public Guid UserId { get; set; }
    public string UserEmail { get; set; } = string.Empty;
    public int RequestCount { get; set; }
    public int ActiveGrants { get; set; }
}

/// <summary>
/// Template usage statistics
/// </summary>
public class JitAccessTemplateUsageDto
{
    public Guid TemplateId { get; set; }
    public string TemplateName { get; set; } = string.Empty;
    public int UsageCount { get; set; }
    public DateTime? LastUsed { get; set; }
}
