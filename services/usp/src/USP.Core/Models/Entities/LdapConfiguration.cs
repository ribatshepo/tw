namespace USP.Core.Models.Entities;

/// <summary>
/// LDAP/Active Directory server configuration for enterprise directory integration
/// </summary>
public class LdapConfiguration
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;

    // Connection Settings
    public string ServerUrl { get; set; } = string.Empty;
    public int Port { get; set; } = 389;
    public bool UseSsl { get; set; } = true;
    public bool UseTls { get; set; } = false;

    // Bind Settings
    public string BaseDn { get; set; } = string.Empty;
    public string BindDn { get; set; } = string.Empty;
    public string BindPassword { get; set; } = string.Empty; // Encrypted with master key

    // Search Filters
    public string UserSearchFilter { get; set; } = "(sAMAccountName={0})";
    public string UserSearchBase { get; set; } = string.Empty; // If different from BaseDn
    public string? GroupSearchFilter { get; set; } = "(objectClass=group)";
    public string? GroupSearchBase { get; set; }

    // Attribute Mappings
    public string EmailAttribute { get; set; } = "mail";
    public string FirstNameAttribute { get; set; } = "givenName";
    public string LastNameAttribute { get; set; } = "sn";
    public string UsernameAttribute { get; set; } = "sAMAccountName";
    public string GroupMembershipAttribute { get; set; } = "memberOf";

    // JIT Provisioning
    public bool EnableJitProvisioning { get; set; } = true;
    public Guid? DefaultRoleId { get; set; }
    public bool SyncGroupsAsRoles { get; set; } = true;
    public bool UpdateUserOnLogin { get; set; } = true;

    // Group Sync Settings
    public bool EnableGroupSync { get; set; } = true;
    public int GroupSyncIntervalMinutes { get; set; } = 60;
    public DateTime? LastGroupSync { get; set; }
    public bool NestedGroupsEnabled { get; set; } = true;

    // Group to Role Mapping (JSON)
    // Example: {"CN=Admins,OU=Groups,DC=example,DC=com": "admin"}
    public string? GroupRoleMapping { get; set; }

    // Status
    public bool IsActive { get; set; } = true;
    public string? LastTestResult { get; set; }
    public DateTime? LastTestedAt { get; set; }

    // Audit
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public ApplicationUser Creator { get; set; } = null!;
}
