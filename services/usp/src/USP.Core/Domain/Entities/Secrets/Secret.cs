using System.ComponentModel.DataAnnotations;
using USP.Core.Domain.Enums;

namespace USP.Core.Domain.Entities.Secrets;

/// <summary>
/// Represents a secret stored in the vault with versioning support
/// </summary>
public class Secret
{
    /// <summary>
    /// Unique identifier for the secret
    /// </summary>
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Path to the secret (e.g., "secret/data/prod/database/credentials")
    /// </summary>
    [Required]
    [MaxLength(500)]
    public string Path { get; set; } = null!;

    /// <summary>
    /// Type of secret
    /// </summary>
    [Required]
    public SecretType Type { get; set; }

    /// <summary>
    /// Current version number
    /// </summary>
    public int CurrentVersion { get; set; } = 1;

    /// <summary>
    /// Maximum number of versions to retain (0 = unlimited)
    /// </summary>
    public int MaxVersions { get; set; } = 10;

    /// <summary>
    /// Indicates whether Check-And-Set (CAS) is required for updates
    /// </summary>
    public bool CasRequired { get; set; }

    /// <summary>
    /// Indicates whether the secret is soft-deleted
    /// </summary>
    public bool IsDeleted { get; set; }

    /// <summary>
    /// Metadata stored as JSON
    /// </summary>
    public string? Metadata { get; set; }

    /// <summary>
    /// Timestamp when the secret was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// User ID who created the secret
    /// </summary>
    public string? CreatedBy { get; set; }

    /// <summary>
    /// Timestamp when the secret was last updated
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// User ID who last updated the secret
    /// </summary>
    public string? UpdatedBy { get; set; }

    /// <summary>
    /// Timestamp when the secret was soft-deleted
    /// </summary>
    public DateTime? DeletedAt { get; set; }

    // Navigation properties

    /// <summary>
    /// All versions of this secret
    /// </summary>
    public virtual ICollection<SecretVersion> Versions { get; set; } = new List<SecretVersion>();
}
