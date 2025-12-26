namespace USP.API.DTOs.Session;

/// <summary>
/// Response model for session information.
/// </summary>
public class SessionResponse
{
    /// <summary>
    /// Session unique identifier.
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// User ID associated with this session.
    /// </summary>
    public required string UserId { get; set; }

    /// <summary>
    /// Client's IP address.
    /// </summary>
    public required string IpAddress { get; set; }

    /// <summary>
    /// Client's user agent (browser/device information).
    /// </summary>
    public required string UserAgent { get; set; }

    /// <summary>
    /// Device fingerprint (optional).
    /// </summary>
    public string? DeviceFingerprint { get; set; }

    /// <summary>
    /// Parsed device information from user agent.
    /// </summary>
    public string DeviceInfo { get; set; } = string.Empty;

    /// <summary>
    /// When the session was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the session was last active.
    /// </summary>
    public DateTime LastActivityAt { get; set; }

    /// <summary>
    /// When the session expires.
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Whether the session has been revoked.
    /// </summary>
    public bool IsRevoked { get; set; }

    /// <summary>
    /// When the session was revoked (if applicable).
    /// </summary>
    public DateTime? RevokedAt { get; set; }
}
