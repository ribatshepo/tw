namespace USP.Core.Models.Entities;

/// <summary>
/// SCIM 2.0 group entity for group management and provisioning
/// Maps to ASP.NET Identity Role but supports SCIM schema
/// </summary>
public class ScimGroup
{
    public Guid Id { get; set; }
    public Guid RoleId { get; set; }

    // SCIM Core Schema attributes
    public string DisplayName { get; set; } = string.Empty;
    public string? ExternalId { get; set; }

    // SCIM metadata
    public string ResourceType { get; set; } = "Group";
    public DateTime Created { get; set; } = DateTime.UtcNow;
    public DateTime LastModified { get; set; } = DateTime.UtcNow;
    public string? Location { get; set; }
    public string Version { get; set; } = "1";

    // Provisioning metadata
    public string? ProvisioningSource { get; set; }
    public DateTime? LastSyncedAt { get; set; }
    public string? SyncStatus { get; set; }
    public string? SyncErrorMessage { get; set; }

    // Navigation properties
    public virtual Role Role { get; set; } = null!;
    public virtual ICollection<ScimGroupMembership> Members { get; set; } = new List<ScimGroupMembership>();
}
