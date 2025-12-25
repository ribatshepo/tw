using USP.Core.Models.DTOs.PAM;

namespace USP.Core.Services.PAM;

/// <summary>
/// Service for managing privileged account checkout/checkin operations
/// </summary>
public interface ICheckoutService
{
    /// <summary>
    /// Request checkout of a privileged account
    /// </summary>
    Task<CheckoutResponseDto> RequestCheckoutAsync(Guid accountId, Guid userId, CheckoutRequestDto request);

    /// <summary>
    /// Checkin a privileged account
    /// </summary>
    Task<bool> CheckinAccountAsync(Guid checkoutId, Guid userId, CheckinRequestDto? request = null);

    /// <summary>
    /// Get active checkouts for a user
    /// </summary>
    Task<List<AccountCheckoutDto>> GetActiveCheckoutsAsync(Guid userId);

    /// <summary>
    /// Get all checkouts for a user (active and historical)
    /// </summary>
    Task<List<AccountCheckoutDto>> GetCheckoutHistoryAsync(Guid userId, int? limit = 50);

    /// <summary>
    /// Get checkout by ID
    /// </summary>
    Task<AccountCheckoutDto?> GetCheckoutByIdAsync(Guid checkoutId, Guid userId);

    /// <summary>
    /// Extend checkout duration
    /// </summary>
    Task<bool> ExtendCheckoutAsync(Guid checkoutId, Guid userId, int additionalMinutes);

    /// <summary>
    /// Force checkin (admin operation)
    /// </summary>
    Task<bool> ForceCheckinAsync(Guid checkoutId, Guid adminUserId, string reason);

    /// <summary>
    /// Check if account is currently checked out
    /// </summary>
    Task<bool> IsAccountCheckedOutAsync(Guid accountId);

    /// <summary>
    /// Get current checkout for an account (if any)
    /// </summary>
    Task<AccountCheckoutDto?> GetActiveCheckoutForAccountAsync(Guid accountId);

    /// <summary>
    /// Auto-checkin expired checkouts (background job)
    /// </summary>
    Task<int> ProcessExpiredCheckoutsAsync();

    /// <summary>
    /// Get checkout statistics
    /// </summary>
    Task<CheckoutStatisticsDto> GetCheckoutStatisticsAsync(Guid userId);
}
