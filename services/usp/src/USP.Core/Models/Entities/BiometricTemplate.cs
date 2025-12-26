namespace USP.Core.Models.Entities;

/// <summary>
/// Biometric template entity for biometric authentication
/// </summary>
public class BiometricTemplate
{
    /// <summary>
    /// Biometric template ID
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// User ID
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Biometric type (Fingerprint, Face, Iris, Voice)
    /// </summary>
    public string BiometricType { get; set; } = string.Empty;

    /// <summary>
    /// Encrypted biometric template data
    /// </summary>
    public string EncryptedTemplateData { get; set; } = string.Empty;

    /// <summary>
    /// Encryption IV for the template
    /// </summary>
    public string EncryptionIv { get; set; } = string.Empty;

    /// <summary>
    /// Device ID that enrolled this biometric
    /// </summary>
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// Friendly name for this biometric device
    /// </summary>
    public string DeviceName { get; set; } = string.Empty;

    /// <summary>
    /// Whether this biometric is active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Whether this is the primary biometric method
    /// </summary>
    public bool IsPrimary { get; set; }

    /// <summary>
    /// Template quality score (0-100)
    /// </summary>
    public int QualityScore { get; set; }

    /// <summary>
    /// Last time this biometric was used for authentication
    /// </summary>
    public DateTime? LastUsedAt { get; set; }

    /// <summary>
    /// Total authentication count
    /// </summary>
    public int AuthenticationCount { get; set; }

    /// <summary>
    /// Failed authentication attempts (consecutive)
    /// </summary>
    public int FailedAttempts { get; set; }

    /// <summary>
    /// Enrollment timestamp
    /// </summary>
    public DateTime EnrolledAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last updated timestamp
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// PIN hash for hybrid biometric/PIN authentication (BCrypt)
    /// </summary>
    public string? PinHash { get; set; }

    /// <summary>
    /// PIN salt for hybrid authentication
    /// </summary>
    public string? PinSalt { get; set; }

    /// <summary>
    /// Liveness detection score (0-100, higher is better)
    /// </summary>
    public int? LivenessScore { get; set; }

    /// <summary>
    /// Navigation property
    /// </summary>
    public virtual ApplicationUser User { get; set; } = null!;
}
