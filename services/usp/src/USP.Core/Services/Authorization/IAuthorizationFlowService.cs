using USP.Core.Models.DTOs.Authorization;

namespace USP.Core.Services.Authorization;

/// <summary>
/// Service for managing flow-based authorization workflows
/// </summary>
public interface IAuthorizationFlowService
{
    /// <summary>
    /// Initiate an authorization flow
    /// </summary>
    Task<AuthorizationFlowResponse> InitiateFlowAsync(AuthorizationFlowRequest request);

    /// <summary>
    /// Approve a step in the authorization flow
    /// </summary>
    Task<AuthorizationFlowResponse> ApproveAsync(Guid flowInstanceId, Guid approverId, string comment = "");

    /// <summary>
    /// Deny a step in the authorization flow
    /// </summary>
    Task<AuthorizationFlowResponse> DenyAsync(Guid flowInstanceId, Guid approverId, string comment = "");

    /// <summary>
    /// Get status of an authorization flow instance
    /// </summary>
    Task<AuthorizationFlowResponse> GetFlowStatusAsync(Guid flowInstanceId);

    /// <summary>
    /// Get pending approvals for a user
    /// </summary>
    Task<List<AuthorizationFlowResponse>> GetPendingApprovalsAsync(Guid userId);

    /// <summary>
    /// Cancel an authorization flow instance
    /// </summary>
    Task<bool> CancelFlowAsync(Guid flowInstanceId, Guid requesterId);
}
