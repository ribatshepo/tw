using System.ComponentModel.DataAnnotations;

namespace USP.Core.Models.Entities;

/// <summary>
/// Records history of cloud synchronization operations
/// </summary>
public class CloudSyncHistory
{
    [Key]
    public Guid Id { get; set; }

    /// <summary>
    /// Associated sync configuration
    /// </summary>
    [Required]
    public Guid ConfigurationId { get; set; }

    /// <summary>
    /// Sync operation type: Scheduled, Manual, RealTime
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string OperationType { get; set; } = string.Empty;

    /// <summary>
    /// Sync direction: Push, Pull, Bidirectional
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string Direction { get; set; } = string.Empty;

    /// <summary>
    /// Sync status: Success, PartialSuccess, Failed, InProgress
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when sync started
    /// </summary>
    public DateTime StartedAt { get; set; }

    /// <summary>
    /// Timestamp when sync completed
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Duration of sync operation in milliseconds
    /// </summary>
    public long DurationMs { get; set; }

    /// <summary>
    /// Number of secrets pushed to cloud
    /// </summary>
    public int SecretsPushed { get; set; }

    /// <summary>
    /// Number of secrets pulled from cloud
    /// </summary>
    public int SecretsPulled { get; set; }

    /// <summary>
    /// Number of secrets updated
    /// </summary>
    public int SecretsUpdated { get; set; }

    /// <summary>
    /// Number of secrets deleted
    /// </summary>
    public int SecretsDeleted { get; set; }

    /// <summary>
    /// Number of conflicts detected
    /// </summary>
    public int ConflictsDetected { get; set; }

    /// <summary>
    /// Number of conflicts resolved automatically
    /// </summary>
    public int ConflictsResolved { get; set; }

    /// <summary>
    /// Number of errors encountered
    /// </summary>
    public int ErrorsCount { get; set; }

    /// <summary>
    /// Error message if sync failed
    /// </summary>
    [MaxLength(2000)]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Detailed sync log (JSON)
    /// </summary>
    public string? SyncLog { get; set; }

    /// <summary>
    /// User who triggered the sync (null for scheduled)
    /// </summary>
    public Guid? TriggeredByUserId { get; set; }

    public DateTime CreatedAt { get; set; }

    // Navigation properties
    public CloudSyncConfiguration Configuration { get; set; } = null!;
    public ApplicationUser? TriggeredByUser { get; set; }
    public ICollection<CloudSyncConflict> Conflicts { get; set; } = new List<CloudSyncConflict>();
}
