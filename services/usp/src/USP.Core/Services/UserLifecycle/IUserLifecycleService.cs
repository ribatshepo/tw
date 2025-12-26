using USP.Core.Models.DTOs.UserLifecycle;

namespace USP.Core.Services.UserLifecycle;

/// <summary>
/// Service for automated user lifecycle management including provisioning and deprovisioning
/// </summary>
public interface IUserLifecycleService
{
    /// <summary>
    /// Start automated user provisioning workflow
    /// </summary>
    Task<UserProvisioningWorkflowDto> StartProvisioningWorkflowAsync(Guid initiatorUserId, StartProvisioningWorkflowRequest request);

    /// <summary>
    /// Get provisioning workflow status
    /// </summary>
    Task<UserProvisioningWorkflowDto?> GetProvisioningWorkflowAsync(Guid workflowId, Guid userId);

    /// <summary>
    /// Start automated user deprovisioning workflow
    /// </summary>
    Task<UserDeprovisioningWorkflowDto> StartDeprovisioningWorkflowAsync(Guid initiatorUserId, StartDeprovisioningWorkflowRequest request);

    /// <summary>
    /// Get deprovisioning workflow status
    /// </summary>
    Task<UserDeprovisioningWorkflowDto?> GetDeprovisioningWorkflowAsync(Guid workflowId, Guid userId);

    /// <summary>
    /// Create offboarding checklist for user
    /// </summary>
    Task<OffboardingChecklistDto> CreateOffboardingChecklistAsync(Guid userId, Guid initiatorUserId);

    /// <summary>
    /// Get offboarding checklist
    /// </summary>
    Task<OffboardingChecklistDto?> GetOffboardingChecklistAsync(Guid checklistId, Guid userId);

    /// <summary>
    /// Complete offboarding checklist item
    /// </summary>
    Task<bool> CompleteChecklistItemAsync(Guid itemId, Guid userId, CompleteChecklistItemRequest request);

    /// <summary>
    /// Detect orphaned accounts (inactive users)
    /// </summary>
    Task<List<OrphanedAccountDto>> DetectOrphanedAccountsAsync(int inactiveDaysThreshold = 90);

    /// <summary>
    /// Cleanup orphaned account resources
    /// </summary>
    Task<ResourceCleanupResultDto> CleanupOrphanedAccountAsync(Guid userId, Guid initiatorUserId);

    /// <summary>
    /// Apply data retention policy for user
    /// </summary>
    Task<bool> ApplyDataRetentionPolicyAsync(Guid userId, int retentionDays);

    /// <summary>
    /// Process scheduled user lifecycle operations (background job)
    /// </summary>
    Task<int> ProcessScheduledLifecycleOperationsAsync();

    /// <summary>
    /// Get all active provisioning workflows
    /// </summary>
    Task<List<UserProvisioningWorkflowDto>> GetActiveProvisioningWorkflowsAsync(Guid userId);

    /// <summary>
    /// Get all active deprovisioning workflows
    /// </summary>
    Task<List<UserDeprovisioningWorkflowDto>> GetActiveDeprovisioningWorkflowsAsync(Guid userId);
}
