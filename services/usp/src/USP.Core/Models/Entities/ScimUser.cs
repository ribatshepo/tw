namespace USP.Core.Models.Entities;

/// <summary>
/// SCIM 2.0 user entity for user lifecycle management and provisioning
/// Maps to ASP.NET Identity ApplicationUser but supports SCIM schema
/// </summary>
public class ScimUser
{
    public Guid Id { get; set; }
    public Guid ApplicationUserId { get; set; }

    // SCIM Core Schema attributes
    public string UserName { get; set; } = string.Empty;
    public string? ExternalId { get; set; }
    public bool Active { get; set; } = true;

    // Name attributes
    public string? GivenName { get; set; }
    public string? FamilyName { get; set; }
    public string? MiddleName { get; set; }
    public string? HonorificPrefix { get; set; }
    public string? HonorificSuffix { get; set; }
    public string? Formatted { get; set; }

    // Contact information
    public string? DisplayName { get; set; }
    public string? NickName { get; set; }
    public string? ProfileUrl { get; set; }
    public string? Title { get; set; }
    public string? UserType { get; set; }
    public string? PreferredLanguage { get; set; }
    public string? Locale { get; set; }
    public string? Timezone { get; set; }

    // Enterprise extension attributes
    public string? EmployeeNumber { get; set; }
    public string? CostCenter { get; set; }
    public string? Organization { get; set; }
    public string? Division { get; set; }
    public string? Department { get; set; }
    public Guid? ManagerId { get; set; }

    // SCIM metadata
    public string ResourceType { get; set; } = "User";
    public DateTime Created { get; set; } = DateTime.UtcNow;
    public DateTime LastModified { get; set; } = DateTime.UtcNow;
    public string? Location { get; set; }
    public string Version { get; set; } = "1";

    // Provisioning metadata
    public string? ProvisioningSource { get; set; } // e.g., "Workday", "BambooHR", "Okta"
    public DateTime? LastSyncedAt { get; set; }
    public string? SyncStatus { get; set; } // "synced", "pending", "error"
    public string? SyncErrorMessage { get; set; }

    // Navigation properties
    public virtual ApplicationUser ApplicationUser { get; set; } = null!;
    public virtual ScimUser? Manager { get; set; }
    public virtual ICollection<ScimUser> DirectReports { get; set; } = new List<ScimUser>();
    public virtual ICollection<ScimGroupMembership> GroupMemberships { get; set; } = new List<ScimGroupMembership>();
}
