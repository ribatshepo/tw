namespace USP.Core.Models.Entities;

/// <summary>
/// QR code authentication session entity
/// </summary>
public class QrCodeAuth
{
    /// <summary>
    /// QR code authentication ID
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// User ID (null until scanned)
    /// </summary>
    public Guid? UserId { get; set; }

    /// <summary>
    /// QR code token
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// Session ID from the browser requesting QR code
    /// </summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// Whether the QR code has been scanned
    /// </summary>
    public bool IsScanned { get; set; }

    /// <summary>
    /// Whether the user approved the authentication
    /// </summary>
    public bool IsApproved { get; set; }

    /// <summary>
    /// When the QR code was scanned
    /// </summary>
    public DateTime? ScannedAt { get; set; }

    /// <summary>
    /// When the authentication was approved/denied
    /// </summary>
    public DateTime? ApprovedAt { get; set; }

    /// <summary>
    /// Creation timestamp
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Expiration timestamp (typically 5 minutes)
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// IP address of the browser session
    /// </summary>
    public string? BrowserIpAddress { get; set; }

    /// <summary>
    /// IP address of the mobile device that scanned
    /// </summary>
    public string? MobileIpAddress { get; set; }

    /// <summary>
    /// Device fingerprint of mobile device
    /// </summary>
    public string? DeviceFingerprint { get; set; }

    /// <summary>
    /// Navigation property
    /// </summary>
    public virtual ApplicationUser? User { get; set; }
}
