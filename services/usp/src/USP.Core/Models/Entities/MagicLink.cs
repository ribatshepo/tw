namespace USP.Core.Models.Entities;

/// <summary>
/// Magic link entity for passwordless authentication
/// </summary>
public class MagicLink
{
    /// <summary>
    /// Magic link ID
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// User ID
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Email address the link was sent to
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Magic link token
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// Redirect URL after successful authentication
    /// </summary>
    public string? RedirectUrl { get; set; }

    /// <summary>
    /// Whether the link has been used
    /// </summary>
    public bool IsUsed { get; set; }

    /// <summary>
    /// When the link was used
    /// </summary>
    public DateTime? UsedAt { get; set; }

    /// <summary>
    /// Creation timestamp
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Expiration timestamp
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// IP address of the request
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// User agent
    /// </summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// Navigation property
    /// </summary>
    public virtual ApplicationUser User { get; set; } = null!;
}
