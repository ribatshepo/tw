using System.Net;

namespace USP.Core.Models.Entities;

/// <summary>
/// User session entity
/// </summary>
public class Session
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public string? RefreshTokenHash { get; set; }
    public IPAddress? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime LastActivity { get; set; } = DateTime.UtcNow;
    public bool Revoked { get; set; }
    public DateTime? RevokedAt { get; set; }

    // Navigation properties
    public virtual ApplicationUser User { get; set; } = null!;
}
