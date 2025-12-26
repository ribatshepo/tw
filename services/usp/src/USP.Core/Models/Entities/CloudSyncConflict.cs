using System.ComponentModel.DataAnnotations;

namespace USP.Core.Models.Entities;

/// <summary>
/// Represents a synchronization conflict that requires resolution
/// </summary>
public class CloudSyncConflict
{
    [Key]
    public Guid Id { get; set; }

    /// <summary>
    /// Associated sync history record
    /// </summary>
    [Required]
    public Guid SyncHistoryId { get; set; }

    /// <summary>
    /// Associated configuration
    /// </summary>
    [Required]
    public Guid ConfigurationId { get; set; }

    /// <summary>
    /// Secret path where conflict occurred
    /// </summary>
    [Required]
    [MaxLength(500)]
    public string SecretPath { get; set; } = string.Empty;

    /// <summary>
    /// Conflict type: ModifiedBoth, DeletedUsp, DeletedCloud, DifferentVersions
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string ConflictType { get; set; } = string.Empty;

    /// <summary>
    /// Conflict status: Pending, Resolved, Failed
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string Status { get; set; } = "Pending";

    /// <summary>
    /// USP secret value (encrypted)
    /// </summary>
    public string? UspValue { get; set; }

    /// <summary>
    /// USP secret version
    /// </summary>
    public int UspVersion { get; set; }

    /// <summary>
    /// USP secret last modified timestamp
    /// </summary>
    public DateTime UspLastModified { get; set; }

    /// <summary>
    /// Cloud secret value (encrypted)
    /// </summary>
    public string? CloudValue { get; set; }

    /// <summary>
    /// Cloud secret version
    /// </summary>
    [MaxLength(100)]
    public string? CloudVersion { get; set; }

    /// <summary>
    /// Cloud secret last modified timestamp
    /// </summary>
    public DateTime CloudLastModified { get; set; }

    /// <summary>
    /// Resolution strategy applied: LastWriteWins, UspWins, CloudWins, Manual
    /// </summary>
    [MaxLength(50)]
    public string? ResolutionStrategy { get; set; }

    /// <summary>
    /// Chosen value after resolution: Usp, Cloud, Merged
    /// </summary>
    [MaxLength(50)]
    public string? ResolvedValue { get; set; }

    /// <summary>
    /// User who resolved the conflict (if manual)
    /// </summary>
    public Guid? ResolvedByUserId { get; set; }

    /// <summary>
    /// Resolution timestamp
    /// </summary>
    public DateTime? ResolvedAt { get; set; }

    /// <summary>
    /// Resolution notes
    /// </summary>
    [MaxLength(1000)]
    public string? ResolutionNotes { get; set; }

    public DateTime DetectedAt { get; set; }

    // Navigation properties
    public CloudSyncHistory SyncHistory { get; set; } = null!;
    public CloudSyncConfiguration Configuration { get; set; } = null!;
    public ApplicationUser? ResolvedByUser { get; set; }
}
