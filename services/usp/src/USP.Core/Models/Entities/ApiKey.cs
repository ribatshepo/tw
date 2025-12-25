namespace USP.Core.Models.Entities;

/// <summary>
/// API key entity for API authentication
/// </summary>
public class ApiKey
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string KeyHash { get; set; } = string.Empty;
    public string KeyPrefix { get; set; } = string.Empty;
    public string[] Scopes { get; set; } = Array.Empty<string>();
    public DateTime? LastUsedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }
    public bool Revoked { get; set; }
    public DateTime? RevokedAt { get; set; }

    // Navigation properties
    public virtual ApplicationUser User { get; set; } = null!;
}
