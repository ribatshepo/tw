using System.ComponentModel.DataAnnotations;
using USP.Core.Domain.Enums;

namespace USP.Core.Domain.Entities.Identity;

/// <summary>
/// Represents a trusted device that can skip MFA for a limited time
/// </summary>
public class TrustedDevice
{
    /// <summary>
    /// Unique identifier for the trusted device
    /// </summary>
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// User ID this device belongs to
    /// </summary>
    [Required]
    public string UserId { get; set; } = null!;

    /// <summary>
    /// Device fingerprint (hash of user agent, IP, browser features)
    /// </summary>
    [Required]
    [MaxLength(255)]
    public string DeviceFingerprint { get; set; } = null!;

    /// <summary>
    /// Friendly name for the device (e.g., "Chrome on Windows 11")
    /// </summary>
    [Required]
    [MaxLength(255)]
    public string DeviceName { get; set; } = null!;

    /// <summary>
    /// Device type
    /// </summary>
    [Required]
    public DeviceType DeviceType { get; set; }

    /// <summary>
    /// User agent string
    /// </summary>
    [MaxLength(500)]
    public string? UserAgent { get; set; }

    /// <summary>
    /// IP address when device was first trusted
    /// </summary>
    [Required]
    [MaxLength(45)] // IPv6 max length
    public string IpAddress { get; set; } = null!;

    /// <summary>
    /// Geolocation when device was first trusted (city, country)
    /// </summary>
    [MaxLength(255)]
    public string? Location { get; set; }

    /// <summary>
    /// Indicates whether trust is currently active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Number of times this device has been used for authentication
    /// </summary>
    public int UsageCount { get; set; }

    /// <summary>
    /// Last successful authentication timestamp from this device
    /// </summary>
    public DateTime? LastUsedAt { get; set; }

    /// <summary>
    /// Timestamp when the device was first trusted
    /// </summary>
    public DateTime TrustedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Timestamp when the trust expires (null = never expires, configurable per user)
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Timestamp when the record was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Timestamp when the record was last updated
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Soft delete timestamp (null if not deleted)
    /// </summary>
    public DateTime? DeletedAt { get; set; }

    // Navigation properties

    /// <summary>
    /// User this device belongs to
    /// </summary>
    public virtual ApplicationUser User { get; set; } = null!;

    /// <summary>
    /// Check if the device trust is still valid
    /// </summary>
    public bool IsTrusted()
    {
        return IsActive &&
               (!ExpiresAt.HasValue || ExpiresAt.Value > DateTime.UtcNow) &&
               DeletedAt == null;
    }
}
