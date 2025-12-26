using System.ComponentModel.DataAnnotations;
using USP.Core.Domain.Enums;

namespace USP.Core.Domain.Entities.Identity;

/// <summary>
/// Represents an enrolled MFA device/method for a user
/// </summary>
public class MFADevice
{
    /// <summary>
    /// Unique identifier for the MFA device
    /// </summary>
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// User ID this device belongs to
    /// </summary>
    [Required]
    public string UserId { get; set; } = null!;

    /// <summary>
    /// MFA method type
    /// </summary>
    [Required]
    public MFAMethod Method { get; set; }

    /// <summary>
    /// Friendly name for the device (e.g., "iPhone 13", "YubiKey 5")
    /// </summary>
    [Required]
    [MaxLength(255)]
    public string DeviceName { get; set; } = null!;

    /// <summary>
    /// Device-specific data (encrypted)
    /// For TOTP: base32 secret
    /// For WebAuthn: credential ID and public key
    /// For SMS/Email: masked phone number or email
    /// For Push: device token
    /// </summary>
    public string? DeviceData { get; set; }

    /// <summary>
    /// Indicates whether this device is verified and can be used
    /// </summary>
    public bool IsVerified { get; set; }

    /// <summary>
    /// Indicates whether this is the primary MFA device
    /// </summary>
    public bool IsPrimary { get; set; }

    /// <summary>
    /// Number of times this device has been used for MFA
    /// </summary>
    public int UsageCount { get; set; }

    /// <summary>
    /// Last successful MFA verification timestamp
    /// </summary>
    public DateTime? LastUsedAt { get; set; }

    /// <summary>
    /// Timestamp when the device was enrolled
    /// </summary>
    public DateTime EnrolledAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Timestamp when the device expires (null = never expires)
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
    /// Check if the device is active and can be used
    /// </summary>
    public bool IsActive()
    {
        return IsVerified &&
               (!ExpiresAt.HasValue || ExpiresAt.Value > DateTime.UtcNow) &&
               DeletedAt == null;
    }
}
