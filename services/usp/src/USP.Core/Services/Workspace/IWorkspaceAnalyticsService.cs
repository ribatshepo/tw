using USP.Core.Models.DTOs.Workspace;

namespace USP.Core.Services.Workspace;

/// <summary>
/// Service for workspace analytics and reporting
/// </summary>
public interface IWorkspaceAnalyticsService
{
    /// <summary>
    /// Get workspace activity summary
    /// </summary>
    Task<WorkspaceActivityDto> GetActivitySummaryAsync(Guid workspaceId, DateTime startDate, DateTime endDate);

    /// <summary>
    /// Get user activity within workspace
    /// </summary>
    Task<List<UserActivityDto>> GetUserActivityAsync(Guid workspaceId, DateTime startDate, DateTime endDate);

    /// <summary>
    /// Get resource consumption metrics
    /// </summary>
    Task<WorkspaceResourceConsumptionDto> GetResourceConsumptionAsync(Guid workspaceId, DateTime startDate, DateTime endDate);

    /// <summary>
    /// Get cost allocation report
    /// </summary>
    Task<WorkspaceCostAllocationDto> GetCostAllocationAsync(Guid workspaceId, DateTime startDate, DateTime endDate);

    /// <summary>
    /// Get workspace health score
    /// </summary>
    Task<WorkspaceHealthDto> GetHealthScoreAsync(Guid workspaceId);

    /// <summary>
    /// Get compliance posture for workspace
    /// </summary>
    Task<WorkspaceComplianceDto> GetCompliancePostureAsync(Guid workspaceId);

    /// <summary>
    /// Get security alerts for workspace
    /// </summary>
    Task<List<WorkspaceSecurityAlertDto>> GetSecurityAlertsAsync(Guid workspaceId, int limit = 50);
}
