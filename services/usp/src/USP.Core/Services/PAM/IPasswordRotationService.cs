using USP.Core.Models.DTOs.PAM;

namespace USP.Core.Services.PAM;

/// <summary>
/// Service for automated password rotation
/// </summary>
public interface IPasswordRotationService
{
    /// <summary>
    /// Manually rotate password for an account
    /// </summary>
    Task<PasswordRotationResultDto> RotatePasswordAsync(Guid accountId, Guid userId);

    /// <summary>
    /// Verify current credentials work
    /// </summary>
    Task<bool> VerifyCredentialsAsync(Guid accountId);

    /// <summary>
    /// Get rotation history for an account
    /// </summary>
    Task<List<PasswordRotationHistoryDto>> GetRotationHistoryAsync(Guid accountId, Guid userId, int? limit = 50);

    /// <summary>
    /// Process scheduled rotations (background job)
    /// </summary>
    Task<int> ProcessScheduledRotationsAsync();

    /// <summary>
    /// Get accounts due for rotation
    /// </summary>
    Task<List<AccountDueForRotationDto>> GetAccountsDueForRotationAsync(Guid userId);

    /// <summary>
    /// Update rotation policy for an account
    /// </summary>
    Task<bool> UpdateRotationPolicyAsync(
        Guid accountId,
        Guid userId,
        string rotationPolicy,
        int rotationIntervalDays);

    /// <summary>
    /// Get rotation statistics
    /// </summary>
    Task<RotationStatisticsDto> GetRotationStatisticsAsync(Guid userId);
}
