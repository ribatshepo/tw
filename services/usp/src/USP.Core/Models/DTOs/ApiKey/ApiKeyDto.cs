namespace USP.Core.Models.DTOs.ApiKey;

/// <summary>
/// API key response DTO (without the actual key)
/// </summary>
public class ApiKeyDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string KeyPrefix { get; set; } = string.Empty;
    public List<string> Scopes { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public bool Revoked { get; set; }
    public DateTime? RevokedAt { get; set; }
    public int? RateLimitPerMinute { get; set; }
    public int? RateLimitPerHour { get; set; }
    public int? RateLimitPerDay { get; set; }
    public int RequestCount { get; set; }
}

/// <summary>
/// Response when creating API key (includes full key - shown only once)
/// </summary>
public class CreateApiKeyResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string KeyPrefix { get; set; } = string.Empty;
    public List<string> Scopes { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string Warning { get; set; } = "This API key will only be shown once. Store it securely.";
}

/// <summary>
/// Request to update API key
/// </summary>
public class UpdateApiKeyRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public List<string>? Scopes { get; set; }
    public int? RateLimitPerMinute { get; set; }
    public int? RateLimitPerHour { get; set; }
    public int? RateLimitPerDay { get; set; }
}

/// <summary>
/// API key usage statistics
/// </summary>
public class ApiKeyUsageDto
{
    public Guid ApiKeyId { get; set; }
    public string KeyPrefix { get; set; } = string.Empty;
    public int TotalRequests { get; set; }
    public int RequestsLastHour { get; set; }
    public int RequestsLastDay { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public List<UsageByEndpoint> UsageByEndpoints { get; set; } = new();
    public List<UsageByDate> UsageByDates { get; set; } = new();
}

public class UsageByEndpoint
{
    public string Endpoint { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class UsageByDate
{
    public DateTime Date { get; set; }
    public int Count { get; set; }
}
