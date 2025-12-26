using System.ComponentModel.DataAnnotations;

namespace USP.Core.Domain.Entities.Secrets;

/// <summary>
/// Represents a specific version of a secret
/// </summary>
public class SecretVersion
{
    /// <summary>
    /// Unique identifier for this version
    /// </summary>
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Secret ID this version belongs to
    /// </summary>
    [Required]
    public string SecretId { get; set; } = null!;

    /// <summary>
    /// Version number
    /// </summary>
    public int Version { get; set; }

    /// <summary>
    /// Encrypted secret data (JSON)
    /// </summary>
    [Required]
    public string EncryptedData { get; set; } = null!;

    /// <summary>
    /// Encryption key ID used to encrypt this version
    /// </summary>
    [Required]
    public string EncryptionKeyId { get; set; } = null!;

    /// <summary>
    /// Indicates whether this version is soft-deleted
    /// </summary>
    public bool IsDeleted { get; set; }

    /// <summary>
    /// Indicates whether this version is destroyed (cannot be recovered)
    /// </summary>
    public bool IsDestroyed { get; set; }

    /// <summary>
    /// TTL for auto-expiration (null = no expiration)
    /// </summary>
    public TimeSpan? TimeToLive { get; set; }

    /// <summary>
    /// Timestamp when this version expires
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Timestamp when this version was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// User ID who created this version
    /// </summary>
    public string? CreatedBy { get; set; }

    /// <summary>
    /// Timestamp when this version was soft-deleted
    /// </summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>
    /// Timestamp when this version was permanently destroyed
    /// </summary>
    public DateTime? DestroyedAt { get; set; }

    // Navigation properties

    /// <summary>
    /// Parent secret
    /// </summary>
    public virtual Secret Secret { get; set; } = null!;

    /// <summary>
    /// Check if this version is accessible
    /// </summary>
    public bool IsAccessible()
    {
        return !IsDeleted && !IsDestroyed &&
               (!ExpiresAt.HasValue || ExpiresAt.Value > DateTime.UtcNow);
    }
}
