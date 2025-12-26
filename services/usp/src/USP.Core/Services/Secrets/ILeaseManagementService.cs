using USP.Core.Models.DTOs.Secrets;

namespace USP.Core.Services.Secrets;

/// <summary>
/// Service for managing time-bound leases for secret access
/// Provides lease creation, renewal, revocation, and automatic expiration handling
/// </summary>
public interface ILeaseManagementService
{
    /// <summary>
    /// Create a new lease for a secret
    /// </summary>
    /// <param name="secretId">Secret to lease</param>
    /// <param name="userId">User requesting the lease</param>
    /// <param name="leaseDurationSeconds">Duration of the lease in seconds</param>
    /// <param name="autoRenewalEnabled">Enable automatic renewal</param>
    /// <param name="maxRenewals">Maximum number of renewals allowed (null = unlimited)</param>
    /// <returns>Created lease information</returns>
    Task<LeaseDto> CreateLeaseAsync(
        Guid secretId,
        Guid userId,
        int leaseDurationSeconds,
        bool autoRenewalEnabled = false,
        int? maxRenewals = null);

    /// <summary>
    /// Renew an existing lease
    /// </summary>
    /// <param name="leaseId">Lease to renew</param>
    /// <param name="userId">User requesting the renewal</param>
    /// <param name="incrementSeconds">Additional seconds to extend the lease (null = use original duration)</param>
    /// <returns>Updated lease information</returns>
    Task<LeaseDto> RenewLeaseAsync(Guid leaseId, Guid userId, int? incrementSeconds = null);

    /// <summary>
    /// Revoke a lease immediately
    /// </summary>
    /// <param name="leaseId">Lease to revoke</param>
    /// <param name="userId">User revoking the lease</param>
    /// <param name="reason">Reason for revocation</param>
    Task RevokeLeaseAsync(Guid leaseId, Guid userId, string? reason = null);

    /// <summary>
    /// Get lease information by ID
    /// </summary>
    /// <param name="leaseId">Lease ID</param>
    /// <param name="userId">User requesting the information</param>
    /// <returns>Lease information</returns>
    Task<LeaseDto> GetLeaseAsync(Guid leaseId, Guid userId);

    /// <summary>
    /// Get all active leases for a user
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="includeExpired">Include expired leases</param>
    /// <returns>List of leases</returns>
    Task<List<LeaseDto>> GetUserLeasesAsync(Guid userId, bool includeExpired = false);

    /// <summary>
    /// Get all leases for a specific secret
    /// </summary>
    /// <param name="secretId">Secret ID</param>
    /// <param name="userId">User requesting the information</param>
    /// <param name="includeExpired">Include expired leases</param>
    /// <returns>List of leases</returns>
    Task<List<LeaseDto>> GetSecretLeasesAsync(Guid secretId, Guid userId, bool includeExpired = false);

    /// <summary>
    /// Get renewal history for a lease
    /// </summary>
    /// <param name="leaseId">Lease ID</param>
    /// <param name="userId">User requesting the information</param>
    /// <returns>List of renewal history entries</returns>
    Task<List<LeaseRenewalHistoryDto>> GetLeaseRenewalHistoryAsync(Guid leaseId, Guid userId);

    /// <summary>
    /// Handle expiring leases (background job - runs every 15 minutes)
    /// Marks expired leases as expired and sends notifications
    /// </summary>
    Task HandleExpiringLeasesAsync();

    /// <summary>
    /// Process auto-renewals for leases (background job - runs every 10 minutes)
    /// Automatically renews leases that are about to expire and have auto-renewal enabled
    /// </summary>
    Task ProcessAutoRenewalsAsync();

    /// <summary>
    /// Revoke all leases for a specific secret
    /// </summary>
    /// <param name="secretId">Secret ID</param>
    /// <param name="userId">User revoking the leases</param>
    /// <param name="reason">Reason for revocation</param>
    Task RevokeAllSecretLeasesAsync(Guid secretId, Guid userId, string? reason = null);

    /// <summary>
    /// Get lease statistics
    /// </summary>
    /// <param name="userId">User ID (null for all users)</param>
    /// <returns>Lease statistics</returns>
    Task<LeaseStatisticsDto> GetLeaseStatisticsAsync(Guid? userId = null);
}
