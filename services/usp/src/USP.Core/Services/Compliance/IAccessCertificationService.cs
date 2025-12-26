using USP.Core.Models.DTOs.Compliance;

namespace USP.Core.Services.Compliance;

/// <summary>
/// Service for access certification and recertification workflows
/// </summary>
public interface IAccessCertificationService
{
    /// <summary>
    /// Create certification campaign
    /// </summary>
    Task<CertificationCampaignDto> CreateCampaignAsync(CreateCertificationCampaignRequest request, Guid initiatedBy);

    /// <summary>
    /// Get campaign by ID
    /// </summary>
    Task<CertificationCampaignDto?> GetCampaignAsync(Guid campaignId);

    /// <summary>
    /// List all campaigns
    /// </summary>
    Task<List<CertificationCampaignDto>> ListCampaignsAsync(string? status = null);

    /// <summary>
    /// Start certification campaign
    /// </summary>
    Task StartCampaignAsync(Guid campaignId, Guid startedBy);

    /// <summary>
    /// Close certification campaign
    /// </summary>
    Task CloseCampaignAsync(Guid campaignId, Guid closedBy);

    /// <summary>
    /// Get pending reviews for a user (manager)
    /// </summary>
    Task<List<AccessReviewDto>> GetPendingReviewsAsync(Guid reviewerId);

    /// <summary>
    /// Certify (approve) access
    /// </summary>
    Task CertifyAccessAsync(Guid reviewId, CertifyAccessRequest request, Guid reviewerId);

    /// <summary>
    /// Revoke access during certification
    /// </summary>
    Task RevokeAccessAsync(Guid reviewId, RevokeAccessRequest request, Guid reviewerId);

    /// <summary>
    /// Delegate review to another user
    /// </summary>
    Task DelegateReviewAsync(Guid reviewId, Guid delegateToUserId, Guid delegatedBy);

    /// <summary>
    /// Detect orphaned accounts (users without managers or inactive)
    /// </summary>
    Task<List<OrphanedAccountDto>> DetectOrphanedAccountsAsync();

    /// <summary>
    /// Get campaign statistics
    /// </summary>
    Task<CampaignStatisticsDto> GetCampaignStatisticsAsync(Guid campaignId);

    /// <summary>
    /// Auto-revoke access for items not certified within deadline
    /// </summary>
    Task ProcessExpiredReviewsAsync();

    /// <summary>
    /// Send reminder notifications for pending reviews
    /// </summary>
    Task SendReviewRemindersAsync(Guid campaignId);
}
