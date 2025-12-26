using System.ComponentModel.DataAnnotations;

namespace USP.Core.Domain.Entities.Integration;

/// <summary>
/// Represents an API key for programmatic access
/// </summary>
public class ApiKey
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    public string UserId { get; set; } = null!;

    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = null!;

    [Required]
    [MaxLength(255)]
    public string KeyHash { get; set; } = null!;  // Hashed API key

    [MaxLength(100)]
    public string? KeyPrefix { get; set; }  // First 8 chars for identification

    public string? Scopes { get; set; }  // JSON array of allowed scopes

    public bool IsActive { get; set; } = true;

    public int? RateLimitPerMinute { get; set; }

    public string? AllowedIps { get; set; }  // JSON array of IP addresses

    public DateTime? ExpiresAt { get; set; }

    public DateTime? LastUsedAt { get; set; }

    public int UsageCount { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? DeletedAt { get; set; }
}
