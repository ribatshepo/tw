namespace USP.Core.Models.Entities;

/// <summary>
/// Configuration for SCIM synchronization with external identity providers or HR systems
/// </summary>
public class ScimSyncConfiguration
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ProviderType { get; set; } = string.Empty; // "Workday", "BambooHR", "ADP", "Okta", "AzureAD"
    public bool Enabled { get; set; } = true;
    public string Direction { get; set; } = "inbound"; // "inbound", "outbound", "bidirectional"

    // Connection settings (stored as JSON)
    public string ConnectionSettings { get; set; } = "{}";

    // Sync schedule
    public string SyncSchedule { get; set; } = "0 */6 * * *"; // Cron expression, default: every 6 hours
    public DateTime? LastSyncedAt { get; set; }
    public DateTime? NextSyncAt { get; set; }
    public string? LastSyncStatus { get; set; } // "success", "partial", "failed"
    public string? LastSyncErrorMessage { get; set; }
    public int UsersCreated { get; set; }
    public int UsersUpdated { get; set; }
    public int UsersDeactivated { get; set; }
    public int GroupsCreated { get; set; }
    public int GroupsUpdated { get; set; }

    // Attribute mapping (stored as JSON)
    public string AttributeMapping { get; set; } = "{}";

    // Conflict resolution strategy
    public string ConflictResolution { get; set; } = "source_wins"; // "source_wins", "destination_wins", "manual"

    // Filter for selective sync
    public string? SyncFilter { get; set; } // SCIM filter expression

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Guid CreatedBy { get; set; }
}
