using USP.Core.Models.DTOs.PAM;

namespace USP.Core.Services.PAM;

/// <summary>
/// Service for dual control and approval workflows
/// </summary>
public interface IDualControlService
{
    /// <summary>
    /// Create an approval request
    /// </summary>
    Task<AccessApprovalDto> CreateApprovalRequestAsync(CreateApprovalRequest request, Guid requesterId);

    /// <summary>
    /// Approve an approval request
    /// </summary>
    Task<bool> ApproveAsync(Guid approvalId, Guid approverId, string? notes = null);

    /// <summary>
    /// Deny an approval request
    /// </summary>
    Task<bool> DenyAsync(Guid approvalId, Guid approverId, string reason);

    /// <summary>
    /// Get pending approvals for a specific user (as approver)
    /// </summary>
    Task<List<AccessApprovalDto>> GetPendingApprovalsAsync(Guid userId);

    /// <summary>
    /// Get approval requests created by a user
    /// </summary>
    Task<List<AccessApprovalDto>> GetMyRequestsAsync(Guid userId);

    /// <summary>
    /// Get approval by ID
    /// </summary>
    Task<AccessApprovalDto?> GetApprovalByIdAsync(Guid approvalId, Guid userId);

    /// <summary>
    /// Check if approval is complete (approved or denied)
    /// </summary>
    Task<bool> IsApprovalCompleteAsync(Guid approvalId);

    /// <summary>
    /// Cancel an approval request (requester only)
    /// </summary>
    Task<bool> CancelApprovalAsync(Guid approvalId, Guid userId);

    /// <summary>
    /// Process expired approvals (background job)
    /// </summary>
    Task<int> ProcessExpiredApprovalsAsync();

    /// <summary>
    /// Get approval statistics
    /// </summary>
    Task<ApprovalStatisticsDto> GetApprovalStatisticsAsync(Guid userId);
}
