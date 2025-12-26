using System.ComponentModel.DataAnnotations;

namespace USP.Core.Models.Entities;

/// <summary>
/// Represents a cloud provider synchronization configuration
/// </summary>
public class CloudSyncConfiguration
{
    [Key]
    public Guid Id { get; set; }

    /// <summary>
    /// Configuration name
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Cloud provider: AWS, Azure, GCP
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// Sync mode: Push, Pull, Bidirectional
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string SyncMode { get; set; } = string.Empty;

    /// <summary>
    /// Whether sync is enabled
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// AWS region (for AWS only)
    /// </summary>
    [MaxLength(50)]
    public string? AwsRegion { get; set; }

    /// <summary>
    /// AWS access key ID (encrypted)
    /// </summary>
    [MaxLength(500)]
    public string? AwsAccessKeyId { get; set; }

    /// <summary>
    /// AWS secret access key (encrypted)
    /// </summary>
    [MaxLength(1000)]
    public string? AwsSecretAccessKey { get; set; }

    /// <summary>
    /// AWS IAM role ARN (for IAM role-based auth)
    /// </summary>
    [MaxLength(500)]
    public string? AwsIamRoleArn { get; set; }

    /// <summary>
    /// Azure Key Vault URI
    /// </summary>
    [MaxLength(500)]
    public string? AzureKeyVaultUri { get; set; }

    /// <summary>
    /// Azure tenant ID
    /// </summary>
    [MaxLength(100)]
    public string? AzureTenantId { get; set; }

    /// <summary>
    /// Azure client ID (service principal)
    /// </summary>
    [MaxLength(100)]
    public string? AzureClientId { get; set; }

    /// <summary>
    /// Azure client secret (encrypted)
    /// </summary>
    [MaxLength(1000)]
    public string? AzureClientSecret { get; set; }

    /// <summary>
    /// GCP project ID
    /// </summary>
    [MaxLength(100)]
    public string? GcpProjectId { get; set; }

    /// <summary>
    /// GCP service account JSON (encrypted)
    /// </summary>
    public string? GcpServiceAccountJson { get; set; }

    /// <summary>
    /// Encrypted credentials as JSON (consolidated storage for all provider credentials)
    /// </summary>
    public string? EncryptedCredentials { get; set; }

    /// <summary>
    /// Path prefix filter (sync only secrets matching this path)
    /// </summary>
    [MaxLength(500)]
    public string? PathFilter { get; set; }

    /// <summary>
    /// Tag filter (sync only secrets with matching tags)
    /// </summary>
    [MaxLength(500)]
    public string? TagFilter { get; set; }

    /// <summary>
    /// Sync schedule (cron expression)
    /// </summary>
    [MaxLength(100)]
    public string? SyncSchedule { get; set; }

    /// <summary>
    /// Conflict resolution strategy: LastWriteWins, UspWins, CloudWins, Manual
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string ConflictResolution { get; set; } = "LastWriteWins";

    /// <summary>
    /// Whether to enable real-time sync via webhooks
    /// </summary>
    public bool EnableRealTimeSync { get; set; }

    /// <summary>
    /// Last successful sync timestamp
    /// </summary>
    public DateTime? LastSyncAt { get; set; }

    /// <summary>
    /// Next scheduled sync timestamp
    /// </summary>
    public DateTime? NextSyncAt { get; set; }

    /// <summary>
    /// Sync error message from last attempt
    /// </summary>
    [MaxLength(2000)]
    public string? LastSyncError { get; set; }

    /// <summary>
    /// Number of consecutive sync failures
    /// </summary>
    public int ConsecutiveFailures { get; set; }

    /// <summary>
    /// User who created this configuration
    /// </summary>
    [Required]
    public Guid CreatedByUserId { get; set; }

    /// <summary>
    /// User who last modified this configuration
    /// </summary>
    public Guid? UpdatedByUserId { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public ApplicationUser CreatedByUser { get; set; } = null!;
    public ApplicationUser? UpdatedByUser { get; set; }
    public ICollection<CloudSyncHistory> SyncHistory { get; set; } = new List<CloudSyncHistory>();
}
