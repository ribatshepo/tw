using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using USP.Core.Models.DTOs.ApiKey;
using USP.Core.Services.ApiKey;
using USP.Infrastructure.Data;
using ApiKeyEntity = USP.Core.Models.Entities.ApiKey;

namespace USP.Infrastructure.Services.ApiKey;

/// <summary>
/// API key management service with rate limiting and usage tracking
/// </summary>
public class ApiKeyManagementService : IApiKeyManagementService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ApiKeyManagementService> _logger;
    private readonly IDistributedCache _cache;
    private readonly IConfiguration _configuration;

    private const string ApiKeyPrefix = "usp";
    private const int ApiKeyRandomLength = 32;
    private const string RateLimitCacheKeyPrefix = "apikey:ratelimit:";

    public ApiKeyManagementService(
        ApplicationDbContext context,
        ILogger<ApiKeyManagementService> logger,
        IDistributedCache cache,
        IConfiguration configuration)
    {
        _context = context;
        _logger = logger;
        _cache = cache;
        _configuration = configuration;
    }

    public async Task<CreateApiKeyResponse> CreateApiKeyAsync(Guid userId, CreateApiKeyRequest request)
    {
        var environment = _configuration["Environment"] ?? "dev";
        var fullKey = GenerateApiKey(environment);
        var keyHash = HashApiKey(fullKey);
        var keyPrefix = ExtractKeyPrefix(fullKey);
        var signingSecret = GenerateSigningSecret();

        var apiKey = new ApiKeyEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = request.Name,
            Description = request.Description,
            KeyHash = keyHash,
            KeyPrefix = keyPrefix,
            Scopes = request.Scopes.ToArray(),
            SigningSecret = signingSecret,
            RateLimitPerMinute = request.RateLimitPerMinute,
            RateLimitPerHour = request.RateLimitPerHour,
            RateLimitPerDay = request.RateLimitPerDay,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = request.ExpiresAt,
            Revoked = false
        };

        _context.ApiKeys.Add(apiKey);
        await _context.SaveChangesAsync();

        _logger.LogInformation("API key created for user {UserId}: {KeyPrefix}", userId, keyPrefix);

        return new CreateApiKeyResponse
        {
            Id = apiKey.Id,
            Name = apiKey.Name,
            ApiKey = fullKey,
            KeyPrefix = keyPrefix,
            Scopes = request.Scopes,
            CreatedAt = apiKey.CreatedAt,
            ExpiresAt = apiKey.ExpiresAt
        };
    }

    public async Task<IEnumerable<ApiKeyDto>> GetUserApiKeysAsync(Guid userId)
    {
        var apiKeys = await _context.ApiKeys
            .Where(k => k.UserId == userId)
            .OrderByDescending(k => k.CreatedAt)
            .ToListAsync();

        return apiKeys.Select(MapToDto);
    }

    public async Task<ApiKeyDto?> GetApiKeyByIdAsync(Guid userId, Guid apiKeyId)
    {
        var apiKey = await _context.ApiKeys
            .FirstOrDefaultAsync(k => k.Id == apiKeyId && k.UserId == userId);

        return apiKey != null ? MapToDto(apiKey) : null;
    }

    public async Task<ApiKeyDto?> GetApiKeyAsync(string apiKeyId)
    {
        if (!Guid.TryParse(apiKeyId, out var guid))
        {
            _logger.LogWarning("Invalid API key ID format: {ApiKeyId}", apiKeyId);
            return null;
        }

        var apiKey = await _context.ApiKeys
            .FirstOrDefaultAsync(k => k.Id == guid);

        return apiKey != null ? MapToDto(apiKey) : null;
    }

    public async Task<bool> UpdateApiKeyAsync(Guid userId, Guid apiKeyId, UpdateApiKeyRequest request)
    {
        var apiKey = await _context.ApiKeys
            .FirstOrDefaultAsync(k => k.Id == apiKeyId && k.UserId == userId);

        if (apiKey == null)
        {
            return false;
        }

        if (!string.IsNullOrEmpty(request.Name))
        {
            apiKey.Name = request.Name;
        }

        if (request.Description != null)
        {
            apiKey.Description = request.Description;
        }

        if (request.Scopes != null)
        {
            apiKey.Scopes = request.Scopes.ToArray();
        }

        if (request.RateLimitPerMinute.HasValue)
        {
            apiKey.RateLimitPerMinute = request.RateLimitPerMinute;
        }

        if (request.RateLimitPerHour.HasValue)
        {
            apiKey.RateLimitPerHour = request.RateLimitPerHour;
        }

        if (request.RateLimitPerDay.HasValue)
        {
            apiKey.RateLimitPerDay = request.RateLimitPerDay;
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("API key {KeyId} updated for user {UserId}", apiKeyId, userId);

        return true;
    }

    public async Task<bool> RevokeApiKeyAsync(Guid userId, Guid apiKeyId)
    {
        var apiKey = await _context.ApiKeys
            .FirstOrDefaultAsync(k => k.Id == apiKeyId && k.UserId == userId);

        if (apiKey == null)
        {
            return false;
        }

        apiKey.Revoked = true;
        apiKey.RevokedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("API key {KeyId} revoked for user {UserId}", apiKeyId, userId);

        return true;
    }

    public async Task<CreateApiKeyResponse> RotateApiKeyAsync(Guid userId, Guid apiKeyId)
    {
        var oldKey = await _context.ApiKeys
            .FirstOrDefaultAsync(k => k.Id == apiKeyId && k.UserId == userId);

        if (oldKey == null)
        {
            throw new InvalidOperationException("API key not found");
        }

        // Create new key with same settings
        var newKeyRequest = new CreateApiKeyRequest
        {
            Name = $"{oldKey.Name} (Rotated)",
            Description = oldKey.Description,
            Scopes = oldKey.Scopes.ToList(),
            ExpiresAt = oldKey.ExpiresAt,
            RateLimitPerMinute = oldKey.RateLimitPerMinute,
            RateLimitPerHour = oldKey.RateLimitPerHour,
            RateLimitPerDay = oldKey.RateLimitPerDay
        };

        var newKey = await CreateApiKeyAsync(userId, newKeyRequest);

        // Revoke old key
        await RevokeApiKeyAsync(userId, apiKeyId);

        _logger.LogInformation("API key {OldKeyId} rotated to {NewKeyId} for user {UserId}",
            apiKeyId, newKey.Id, userId);

        return newKey;
    }

    public async Task<(Guid UserId, List<string> Scopes, Guid ApiKeyId)?> ValidateApiKeyAsync(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return null;
        }

        var keyHash = HashApiKey(apiKey);

        var key = await _context.ApiKeys
            .FirstOrDefaultAsync(k => k.KeyHash == keyHash && !k.Revoked);

        if (key == null)
        {
            _logger.LogWarning("Invalid API key attempt: {KeyPrefix}", ExtractKeyPrefix(apiKey));
            return null;
        }

        // Check expiration
        if (key.ExpiresAt.HasValue && key.ExpiresAt.Value < DateTime.UtcNow)
        {
            _logger.LogWarning("Expired API key used: {KeyPrefix}", key.KeyPrefix);
            return null;
        }

        // Update last used timestamp
        key.LastUsedAt = DateTime.UtcNow;
        key.RequestCount++;
        await _context.SaveChangesAsync();

        return (key.UserId, key.Scopes.ToList(), key.Id);
    }

    public async Task<bool> CheckRateLimitAsync(Guid apiKeyId)
    {
        var apiKey = await _context.ApiKeys.FindAsync(apiKeyId);

        if (apiKey == null)
        {
            return false;
        }

        // Check minute rate limit
        if (apiKey.RateLimitPerMinute.HasValue)
        {
            var minuteKey = $"{RateLimitCacheKeyPrefix}min:{apiKeyId}";
            var minuteCount = await GetRateLimitCountAsync(minuteKey);

            if (minuteCount >= apiKey.RateLimitPerMinute.Value)
            {
                _logger.LogWarning("API key {KeyId} exceeded minute rate limit: {Count}/{Limit}",
                    apiKeyId, minuteCount, apiKey.RateLimitPerMinute.Value);
                return false;
            }

            await IncrementRateLimitAsync(minuteKey, TimeSpan.FromMinutes(1));
        }

        // Check hour rate limit
        if (apiKey.RateLimitPerHour.HasValue)
        {
            var hourKey = $"{RateLimitCacheKeyPrefix}hour:{apiKeyId}";
            var hourCount = await GetRateLimitCountAsync(hourKey);

            if (hourCount >= apiKey.RateLimitPerHour.Value)
            {
                _logger.LogWarning("API key {KeyId} exceeded hour rate limit: {Count}/{Limit}",
                    apiKeyId, hourCount, apiKey.RateLimitPerHour.Value);
                return false;
            }

            await IncrementRateLimitAsync(hourKey, TimeSpan.FromHours(1));
        }

        // Check day rate limit
        if (apiKey.RateLimitPerDay.HasValue)
        {
            var dayKey = $"{RateLimitCacheKeyPrefix}day:{apiKeyId}";
            var dayCount = await GetRateLimitCountAsync(dayKey);

            if (dayCount >= apiKey.RateLimitPerDay.Value)
            {
                _logger.LogWarning("API key {KeyId} exceeded day rate limit: {Count}/{Limit}",
                    apiKeyId, dayCount, apiKey.RateLimitPerDay.Value);
                return false;
            }

            await IncrementRateLimitAsync(dayKey, TimeSpan.FromDays(1));
        }

        return true;
    }

    public async Task RecordUsageAsync(Guid apiKeyId, string endpoint)
    {
        var apiKey = await _context.ApiKeys.FindAsync(apiKeyId);

        if (apiKey != null)
        {
            apiKey.LastUsedAt = DateTime.UtcNow;
            apiKey.RequestCount++;
            await _context.SaveChangesAsync();
        }

        // Could also store detailed usage logs in a separate table for analytics
    }

    public async Task<ApiKeyUsageDto?> GetUsageStatisticsAsync(Guid userId, Guid apiKeyId)
    {
        var apiKey = await _context.ApiKeys
            .FirstOrDefaultAsync(k => k.Id == apiKeyId && k.UserId == userId);

        if (apiKey == null)
        {
            return null;
        }

        var minuteKey = $"{RateLimitCacheKeyPrefix}min:{apiKeyId}";
        var hourKey = $"{RateLimitCacheKeyPrefix}hour:{apiKeyId}";
        var dayKey = $"{RateLimitCacheKeyPrefix}day:{apiKeyId}";

        var requestsLastHour = await GetRateLimitCountAsync(hourKey);
        var requestsLastDay = await GetRateLimitCountAsync(dayKey);

        return new ApiKeyUsageDto
        {
            ApiKeyId = apiKey.Id,
            KeyPrefix = apiKey.KeyPrefix,
            TotalRequests = apiKey.RequestCount,
            RequestsLastHour = requestsLastHour,
            RequestsLastDay = requestsLastDay,
            LastUsedAt = apiKey.LastUsedAt,
            UsageByEndpoints = new List<UsageByEndpoint>(),
            UsageByDates = new List<UsageByDate>()
        };
    }

    public async Task CleanupExpiredApiKeysAsync()
    {
        var expiredKeys = await _context.ApiKeys
            .Where(k => k.ExpiresAt.HasValue && k.ExpiresAt.Value < DateTime.UtcNow && !k.Revoked)
            .ToListAsync();

        foreach (var key in expiredKeys)
        {
            key.Revoked = true;
            key.RevokedAt = DateTime.UtcNow;
        }

        if (expiredKeys.Any())
        {
            await _context.SaveChangesAsync();
            _logger.LogInformation("Cleaned up {Count} expired API keys", expiredKeys.Count);
        }
    }

    #region Private Helper Methods

    private static string GenerateApiKey(string environment)
    {
        var randomBytes = new byte[ApiKeyRandomLength];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);

        var randomPart = Convert.ToBase64String(randomBytes)
            .Replace("+", "")
            .Replace("/", "")
            .Replace("=", "")
            .Substring(0, ApiKeyRandomLength);

        return $"{ApiKeyPrefix}_{environment}_{randomPart}";
    }

    private static string HashApiKey(string apiKey)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(apiKey);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }

    private static string ExtractKeyPrefix(string apiKey)
    {
        var parts = apiKey.Split('_');
        if (parts.Length >= 3)
        {
            return $"{parts[0]}_{parts[1]}_...{apiKey.Substring(apiKey.Length - 4)}";
        }
        return $"...{apiKey.Substring(Math.Max(0, apiKey.Length - 4))}";
    }

    private static string GenerateSigningSecret()
    {
        var randomBytes = new byte[64]; // 512-bit secret
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }

    private async Task<int> GetRateLimitCountAsync(string cacheKey)
    {
        var cachedValue = await _cache.GetStringAsync(cacheKey);
        return int.TryParse(cachedValue, out var count) ? count : 0;
    }

    private async Task IncrementRateLimitAsync(string cacheKey, TimeSpan expiration)
    {
        var currentCount = await GetRateLimitCountAsync(cacheKey);
        var newCount = currentCount + 1;

        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expiration
        };

        await _cache.SetStringAsync(cacheKey, newCount.ToString(), options);
    }

    private static ApiKeyDto MapToDto(ApiKeyEntity apiKey) => new()
    {
        Id = apiKey.Id,
        Name = apiKey.Name,
        Description = apiKey.Description,
        KeyPrefix = apiKey.KeyPrefix,
        Scopes = apiKey.Scopes.ToList(),
        CreatedAt = apiKey.CreatedAt,
        ExpiresAt = apiKey.ExpiresAt,
        LastUsedAt = apiKey.LastUsedAt,
        Revoked = apiKey.Revoked,
        RevokedAt = apiKey.RevokedAt,
        RateLimitPerMinute = apiKey.RateLimitPerMinute,
        RateLimitPerHour = apiKey.RateLimitPerHour,
        RateLimitPerDay = apiKey.RateLimitPerDay,
        RequestCount = apiKey.RequestCount,
        SigningSecret = apiKey.SigningSecret,
        IsActive = !apiKey.Revoked && (!apiKey.ExpiresAt.HasValue || apiKey.ExpiresAt.Value > DateTime.UtcNow)
    };

    #endregion
}
