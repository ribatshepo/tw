using USP.Core.Models.DTOs.PAM;

namespace USP.Core.Services.PAM;

/// <summary>
/// Service for managing break-glass emergency access
/// </summary>
public interface IBreakGlassService
{
    /// <summary>
    /// Activate break-glass emergency access
    /// </summary>
    Task<BreakGlassAccessDto> ActivateAsync(Guid userId, ActivateBreakGlassRequest request);

    /// <summary>
    /// Deactivate break-glass access
    /// </summary>
    Task<bool> DeactivateAsync(Guid accessId, Guid userId, DeactivateBreakGlassRequest request);

    /// <summary>
    /// Get break-glass access by ID
    /// </summary>
    Task<BreakGlassAccessDto?> GetAccessByIdAsync(Guid accessId, Guid userId);

    /// <summary>
    /// Get active break-glass access for a user
    /// </summary>
    Task<BreakGlassAccessDto?> GetActiveAccessAsync(Guid userId);

    /// <summary>
    /// Get all break-glass access history for a user
    /// </summary>
    Task<List<BreakGlassAccessDto>> GetUserHistoryAsync(Guid userId, int? limit = 50);

    /// <summary>
    /// Get all break-glass access history (admin only)
    /// </summary>
    Task<List<BreakGlassAccessDto>> GetAllHistoryAsync(Guid userId, int? limit = 100);

    /// <summary>
    /// Get break-glass accesses pending review
    /// </summary>
    Task<List<BreakGlassAccessDto>> GetPendingReviewAsync(Guid userId);

    /// <summary>
    /// Review a break-glass access
    /// </summary>
    Task<bool> ReviewAccessAsync(Guid accessId, Guid reviewerId, ReviewBreakGlassRequest request);

    /// <summary>
    /// Process expired break-glass accesses (background job)
    /// </summary>
    Task<int> ProcessExpiredAccessesAsync();

    /// <summary>
    /// Get break-glass access statistics
    /// </summary>
    Task<BreakGlassStatisticsDto> GetStatisticsAsync(Guid userId);

    // Policy Management

    /// <summary>
    /// Get active break-glass policy
    /// </summary>
    Task<BreakGlassPolicyDto?> GetActivePolicyAsync();

    /// <summary>
    /// Create break-glass policy
    /// </summary>
    Task<BreakGlassPolicyDto> CreatePolicyAsync(Guid userId, CreateBreakGlassPolicyRequest request);

    /// <summary>
    /// Update break-glass policy
    /// </summary>
    Task<bool> UpdatePolicyAsync(Guid policyId, Guid userId, CreateBreakGlassPolicyRequest request);

    /// <summary>
    /// Get all break-glass policies
    /// </summary>
    Task<List<BreakGlassPolicyDto>> GetPoliciesAsync(Guid userId);
}
