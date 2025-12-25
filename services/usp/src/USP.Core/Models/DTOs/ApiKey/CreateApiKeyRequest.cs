namespace USP.Core.Models.DTOs.ApiKey;

/// <summary>
/// Request to create a new API key
/// </summary>
public class CreateApiKeyRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<string> Scopes { get; set; } = new();
    public DateTime? ExpiresAt { get; set; }
    public int? RateLimitPerMinute { get; set; }
    public int? RateLimitPerHour { get; set; }
    public int? RateLimitPerDay { get; set; }
}
