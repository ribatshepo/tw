using System.ComponentModel.DataAnnotations;

namespace USP.Core.Domain.Entities.Identity;

/// <summary>
/// Represents an active user session (stored in Redis and PostgreSQL for audit)
/// </summary>
public class Session
{
    /// <summary>
    /// Unique identifier for the session (also used as Redis key)
    /// </summary>
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// User ID this session belongs to
    /// </summary>
    [Required]
    public string UserId { get; set; } = null!;

    /// <summary>
    /// Access token (JWT, encrypted in database)
    /// </summary>
    [Required]
    public string AccessToken { get; set; } = null!;

    /// <summary>
    /// Refresh token (encrypted in database)
    /// </summary>
    [Required]
    public string RefreshToken { get; set; } = null!;

    /// <summary>
    /// Access token expiration timestamp
    /// </summary>
    public DateTime AccessTokenExpiresAt { get; set; }

    /// <summary>
    /// Refresh token expiration timestamp
    /// </summary>
    public DateTime RefreshTokenExpiresAt { get; set; }

    /// <summary>
    /// IP address from which the session was created
    /// </summary>
    [Required]
    [MaxLength(45)] // IPv6 max length
    public string IpAddress { get; set; } = null!;

    /// <summary>
    /// User agent string
    /// </summary>
    [MaxLength(500)]
    public string? UserAgent { get; set; }

    /// <summary>
    /// Device fingerprint (hash of user agent, IP, browser features)
    /// </summary>
    [MaxLength(255)]
    public string? DeviceFingerprint { get; set; }

    /// <summary>
    /// Geolocation (city, country)
    /// </summary>
    [MaxLength(255)]
    public string? Location { get; set; }

    /// <summary>
    /// Indicates whether the session is currently active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Timestamp of last activity (updated on each request)
    /// </summary>
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Timestamp when the session was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Timestamp when the session expires (absolute timeout)
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Timestamp when the session was revoked (null if not revoked)
    /// </summary>
    public DateTime? RevokedAt { get; set; }

    /// <summary>
    /// Reason for revocation (e.g., "User logout", "Admin revoked", "Security alert")
    /// </summary>
    [MaxLength(255)]
    public string? RevocationReason { get; set; }

    /// <summary>
    /// Metadata stored as JSON (custom session data, MFA verification status, etc.)
    /// </summary>
    public string? Metadata { get; set; }

    // Navigation properties

    /// <summary>
    /// User this session belongs to
    /// </summary>
    public virtual ApplicationUser User { get; set; } = null!;

    /// <summary>
    /// Check if the session is valid and can be used
    /// </summary>
    public bool IsValid()
    {
        return IsActive &&
               ExpiresAt > DateTime.UtcNow &&
               RevokedAt == null;
    }

    /// <summary>
    /// Check if the session has exceeded idle timeout (30 minutes)
    /// </summary>
    public bool IsIdle(int idleTimeoutMinutes = 30)
    {
        return DateTime.UtcNow - LastActivityAt > TimeSpan.FromMinutes(idleTimeoutMinutes);
    }

    /// <summary>
    /// Check if the access token has expired
    /// </summary>
    public bool IsAccessTokenExpired()
    {
        return AccessTokenExpiresAt <= DateTime.UtcNow;
    }

    /// <summary>
    /// Check if the refresh token has expired
    /// </summary>
    public bool IsRefreshTokenExpired()
    {
        return RefreshTokenExpiresAt <= DateTime.UtcNow;
    }
}
