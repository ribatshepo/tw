using USP.Core.Models.DTOs.ApiKey;

namespace USP.Core.Services.ApiKey;

/// <summary>
/// Service for API key management and authentication
/// </summary>
public interface IApiKeyManagementService
{
    /// <summary>
    /// Create a new API key for a user
    /// </summary>
    Task<CreateApiKeyResponse> CreateApiKeyAsync(Guid userId, CreateApiKeyRequest request);

    /// <summary>
    /// Get all API keys for a user
    /// </summary>
    Task<IEnumerable<ApiKeyDto>> GetUserApiKeysAsync(Guid userId);

    /// <summary>
    /// Get API key by ID
    /// </summary>
    Task<ApiKeyDto?> GetApiKeyByIdAsync(Guid userId, Guid apiKeyId);

    /// <summary>
    /// Get API key by key ID (without user ID check - for internal use)
    /// </summary>
    Task<ApiKeyDto?> GetApiKeyAsync(string apiKeyId);

    /// <summary>
    /// Update API key
    /// </summary>
    Task<bool> UpdateApiKeyAsync(Guid userId, Guid apiKeyId, UpdateApiKeyRequest request);

    /// <summary>
    /// Revoke API key
    /// </summary>
    Task<bool> RevokeApiKeyAsync(Guid userId, Guid apiKeyId);

    /// <summary>
    /// Rotate API key (revoke old, create new with same settings)
    /// </summary>
    Task<CreateApiKeyResponse> RotateApiKeyAsync(Guid userId, Guid apiKeyId);

    /// <summary>
    /// Validate API key and return user ID and scopes
    /// </summary>
    /// <param name="apiKey">Full API key string</param>
    /// <returns>Tuple of (UserId, Scopes, ApiKeyId) or null if invalid</returns>
    Task<(Guid UserId, List<string> Scopes, Guid ApiKeyId)?> ValidateApiKeyAsync(string apiKey);

    /// <summary>
    /// Check rate limit for API key
    /// </summary>
    /// <returns>True if within rate limit, false if exceeded</returns>
    Task<bool> CheckRateLimitAsync(Guid apiKeyId);

    /// <summary>
    /// Record API key usage
    /// </summary>
    Task RecordUsageAsync(Guid apiKeyId, string endpoint);

    /// <summary>
    /// Get API key usage statistics
    /// </summary>
    Task<ApiKeyUsageDto?> GetUsageStatisticsAsync(Guid userId, Guid apiKeyId);

    /// <summary>
    /// Clean up expired API keys
    /// </summary>
    Task CleanupExpiredApiKeysAsync();
}
