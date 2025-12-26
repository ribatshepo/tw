namespace USP.Core.Models.DTOs.UserLifecycle;

public class UserProvisioningWorkflowDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string WorkflowStatus { get; set; } = string.Empty; // pending, in_progress, completed, failed
    public List<ProvisioningStepDto> Steps { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class ProvisioningStepDto
{
    public string StepName { get; set; } = string.Empty;
    public string StepType { get; set; } = string.Empty; // account_creation, role_assignment, resource_access, notification
    public string Status { get; set; } = string.Empty; // pending, completed, failed, skipped
    public string? ErrorMessage { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class UserDeprovisioningWorkflowDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string WorkflowStatus { get; set; } = string.Empty;
    public List<DeprovisioningStepDto> Steps { get; set; } = new();
    public bool DataRetentionApplied { get; set; }
    public int DataRetentionDays { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class DeprovisioningStepDto
{
    public string StepName { get; set; } = string.Empty;
    public string StepType { get; set; } = string.Empty; // access_revocation, session_termination, data_retention, account_disablement
    public string Status { get; set; } = string.Empty;
    public string? Details { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class OffboardingChecklistDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public List<OffboardingChecklistItemDto> Items { get; set; } = new();
    public int CompletedItems { get; set; }
    public int TotalItems { get; set; }
    public bool IsComplete { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class OffboardingChecklistItemDto
{
    public Guid Id { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty; // access, data, equipment, documentation
    public bool IsCompleted { get; set; }
    public Guid? CompletedByUserId { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Notes { get; set; }
}

public class OrphanedAccountDto
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime? LastLoginAt { get; set; }
    public int DaysSinceLastLogin { get; set; }
    public int ActiveSessions { get; set; }
    public int AssignedResources { get; set; }
    public string AccountStatus { get; set; } = string.Empty;
    public string? DetectionReason { get; set; }
}

public class StartProvisioningWorkflowRequest
{
    public Guid UserId { get; set; }
    public List<string>? RolesToAssign { get; set; }
    public Dictionary<string, string>? ResourceAccess { get; set; }
    public bool SendWelcomeEmail { get; set; } = true;
}

public class StartDeprovisioningWorkflowRequest
{
    public Guid UserId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public int DataRetentionDays { get; set; } = 90;
    public bool ImmediateRevocation { get; set; } = true;
}

public class CompleteChecklistItemRequest
{
    public string? Notes { get; set; }
}

public class ResourceCleanupResultDto
{
    public int SessionsTerminated { get; set; }
    public int AccessPoliciesRevoked { get; set; }
    public int ApiKeysRevoked { get; set; }
    public int SecretsDeleted { get; set; }
    public int FilesArchived { get; set; }
    public TimeSpan CleanupDuration { get; set; }
}
