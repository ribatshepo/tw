using USP.Core.Models.DTOs.Rotation;

namespace USP.Core.Services.Rotation;

/// <summary>
/// Service for automated API key rotation with blue/green deployment support
/// </summary>
public interface IApiKeyRotationService
{
    /// <summary>
    /// Create API key rotation configuration
    /// </summary>
    Task<ApiKeyRotationDto> CreateRotationConfigAsync(Guid userId, CreateApiKeyRotationRequest request);

    /// <summary>
    /// Get API key rotation configuration
    /// </summary>
    Task<ApiKeyRotationDto?> GetRotationConfigAsync(Guid rotationId, Guid userId);

    /// <summary>
    /// Get all API key rotation configurations
    /// </summary>
    Task<List<ApiKeyRotationDto>> GetRotationConfigsAsync(Guid userId, bool? activeOnly = null);

    /// <summary>
    /// Delete API key rotation configuration
    /// </summary>
    Task<bool> DeleteRotationConfigAsync(Guid rotationId, Guid userId);

    /// <summary>
    /// Manually trigger API key rotation
    /// </summary>
    Task<ApiKeyRotationResultDto> RotateApiKeyAsync(Guid rotationId, Guid userId);

    /// <summary>
    /// Process scheduled API key rotations (background job)
    /// </summary>
    Task<int> ProcessScheduledRotationsAsync();

    /// <summary>
    /// Revoke API key immediately
    /// </summary>
    Task<bool> RevokeApiKeyAsync(Guid rotationId, Guid userId, string reason);

    /// <summary>
    /// Get API key usage analytics
    /// </summary>
    Task<ApiKeyUsageDto> GetUsageAnalyticsAsync(Guid rotationId, Guid userId);

    /// <summary>
    /// Promote standby key to active (in blue-green rotation)
    /// </summary>
    Task<bool> PromoteStandbyKeyAsync(Guid rotationId, Guid userId);

    /// <summary>
    /// Get API keys due for rotation
    /// </summary>
    Task<List<ApiKeyRotationDto>> GetKeysDueForRotationAsync(Guid userId);
}
