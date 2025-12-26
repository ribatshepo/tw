namespace USP.Core.Models.DTOs.UserLifecycle;

/// <summary>
/// DTO for SCIM synchronization configuration
/// </summary>
public class ScimSyncConfigurationDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ProviderType { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public string Direction { get; set; } = string.Empty;
    public string SyncSchedule { get; set; } = string.Empty;
    public DateTime? LastSyncedAt { get; set; }
    public DateTime? NextSyncAt { get; set; }
    public string? LastSyncStatus { get; set; }
    public int UsersCreated { get; set; }
    public int UsersUpdated { get; set; }
    public int UsersDeactivated { get; set; }
    public int GroupsCreated { get; set; }
    public int GroupsUpdated { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Request to create SCIM sync configuration
/// </summary>
public class CreateScimSyncConfigurationRequest
{
    public string Name { get; set; } = string.Empty;
    public string ProviderType { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public string Direction { get; set; } = "inbound";
    public Dictionary<string, object> ConnectionSettings { get; set; } = new Dictionary<string, object>();
    public string SyncSchedule { get; set; } = "0 */6 * * *";
    public Dictionary<string, string> AttributeMapping { get; set; } = new Dictionary<string, string>();
    public string ConflictResolution { get; set; } = "source_wins";
    public string? SyncFilter { get; set; }
}

/// <summary>
/// Result of a SCIM sync operation
/// </summary>
public class ScimSyncResultDto
{
    public Guid ConfigurationId { get; set; }
    public bool Success { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime CompletedAt { get; set; }
    public int UsersCreated { get; set; }
    public int UsersUpdated { get; set; }
    public int UsersDeactivated { get; set; }
    public int UsersSynced { get; set; }
    public int GroupsCreated { get; set; }
    public int GroupsUpdated { get; set; }
    public int GroupsSynced { get; set; }
    public List<string> Errors { get; set; } = new List<string>();
}
