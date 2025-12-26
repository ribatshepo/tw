using System.ComponentModel.DataAnnotations;

namespace USP.Core.Models.DTOs.Ldap;

// ====================
// Configuration Requests/Responses
// ====================

/// <summary>
/// Request to configure LDAP server
/// </summary>
public class ConfigureLdapRequest
{
    [Required(ErrorMessage = "Name is required")]
    [StringLength(200, MinimumLength = 1, ErrorMessage = "Name must be between 1 and 200 characters")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Server URL is required")]
    [StringLength(500, MinimumLength = 1, ErrorMessage = "Server URL must be between 1 and 500 characters")]
    public string ServerUrl { get; set; } = string.Empty;

    [Range(1, 65535, ErrorMessage = "Port must be between 1 and 65535")]
    public int Port { get; set; } = 389;

    public bool UseSsl { get; set; } = true;
    public bool UseTls { get; set; } = false;

    [Required(ErrorMessage = "Base DN is required")]
    [StringLength(500, MinimumLength = 1, ErrorMessage = "Base DN must be between 1 and 500 characters")]
    public string BaseDn { get; set; } = string.Empty;

    [Required(ErrorMessage = "Bind DN is required")]
    [StringLength(500, MinimumLength = 1, ErrorMessage = "Bind DN must be between 1 and 500 characters")]
    public string BindDn { get; set; } = string.Empty;

    [Required(ErrorMessage = "Bind password is required")]
    public string BindPassword { get; set; } = string.Empty;

    [Required]
    [StringLength(500)]
    public string UserSearchFilter { get; set; } = "(sAMAccountName={0})";

    [StringLength(500)]
    public string? UserSearchBase { get; set; }

    [StringLength(500)]
    public string? GroupSearchFilter { get; set; } = "(objectClass=group)";

    [StringLength(500)]
    public string? GroupSearchBase { get; set; }

    [Required]
    [StringLength(100)]
    public string EmailAttribute { get; set; } = "mail";

    [Required]
    [StringLength(100)]
    public string FirstNameAttribute { get; set; } = "givenName";

    [Required]
    [StringLength(100)]
    public string LastNameAttribute { get; set; } = "sn";

    [Required]
    [StringLength(100)]
    public string UsernameAttribute { get; set; } = "sAMAccountName";

    [Required]
    [StringLength(100)]
    public string GroupMembershipAttribute { get; set; } = "memberOf";

    public bool EnableJitProvisioning { get; set; } = true;
    public Guid? DefaultRoleId { get; set; }
    public bool SyncGroupsAsRoles { get; set; } = true;
    public bool UpdateUserOnLogin { get; set; } = true;
    public bool EnableGroupSync { get; set; } = true;

    [Range(1, 10080, ErrorMessage = "Group sync interval must be between 1 and 10080 minutes (1 week)")]
    public int GroupSyncIntervalMinutes { get; set; } = 60;

    public bool NestedGroupsEnabled { get; set; } = true;
    public Dictionary<string, string>? GroupRoleMapping { get; set; }
}

/// <summary>
/// Request to update LDAP configuration
/// </summary>
public class UpdateLdapConfigRequest
{
    [StringLength(200, MinimumLength = 1)]
    public string? Name { get; set; }

    [StringLength(500, MinimumLength = 1)]
    public string? ServerUrl { get; set; }

    [Range(1, 65535)]
    public int? Port { get; set; }

    public bool? UseSsl { get; set; }
    public bool? UseTls { get; set; }

    [StringLength(500, MinimumLength = 1)]
    public string? BaseDn { get; set; }

    [StringLength(500, MinimumLength = 1)]
    public string? BindDn { get; set; }

    public string? BindPassword { get; set; }

    [StringLength(500)]
    public string? UserSearchFilter { get; set; }

    [StringLength(500)]
    public string? UserSearchBase { get; set; }

    [StringLength(500)]
    public string? GroupSearchFilter { get; set; }

    [StringLength(500)]
    public string? GroupSearchBase { get; set; }

    [StringLength(100)]
    public string? EmailAttribute { get; set; }

    [StringLength(100)]
    public string? FirstNameAttribute { get; set; }

    [StringLength(100)]
    public string? LastNameAttribute { get; set; }

    [StringLength(100)]
    public string? UsernameAttribute { get; set; }

    [StringLength(100)]
    public string? GroupMembershipAttribute { get; set; }

    public bool? EnableJitProvisioning { get; set; }
    public Guid? DefaultRoleId { get; set; }
    public bool? SyncGroupsAsRoles { get; set; }
    public bool? UpdateUserOnLogin { get; set; }
    public bool? EnableGroupSync { get; set; }

    [Range(1, 10080)]
    public int? GroupSyncIntervalMinutes { get; set; }

    public bool? NestedGroupsEnabled { get; set; }
    public Dictionary<string, string>? GroupRoleMapping { get; set; }
    public bool? IsActive { get; set; }
}

/// <summary>
/// LDAP configuration response
/// </summary>
public class LdapConfigResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ServerUrl { get; set; } = string.Empty;
    public int Port { get; set; }
    public bool UseSsl { get; set; }
    public bool UseTls { get; set; }
    public string BaseDn { get; set; } = string.Empty;
    public string BindDn { get; set; } = string.Empty;
    public string UserSearchFilter { get; set; } = string.Empty;
    public string? UserSearchBase { get; set; }
    public string? GroupSearchFilter { get; set; }
    public string? GroupSearchBase { get; set; }
    public string EmailAttribute { get; set; } = string.Empty;
    public string FirstNameAttribute { get; set; } = string.Empty;
    public string LastNameAttribute { get; set; } = string.Empty;
    public string UsernameAttribute { get; set; } = string.Empty;
    public string GroupMembershipAttribute { get; set; } = string.Empty;
    public bool EnableJitProvisioning { get; set; }
    public Guid? DefaultRoleId { get; set; }
    public bool SyncGroupsAsRoles { get; set; }
    public bool UpdateUserOnLogin { get; set; }
    public bool EnableGroupSync { get; set; }
    public int GroupSyncIntervalMinutes { get; set; }
    public DateTime? LastGroupSync { get; set; }
    public bool NestedGroupsEnabled { get; set; }
    public Dictionary<string, string>? GroupRoleMapping { get; set; }
    public bool IsActive { get; set; }
    public string? LastTestResult { get; set; }
    public DateTime? LastTestedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// List of LDAP configurations
/// </summary>
public class ListLdapConfigsResponse
{
    public List<LdapConfigResponse> Configurations { get; set; } = new();
    public int Total { get; set; }
}

// ====================
// Authentication Requests/Responses
// ====================

/// <summary>
/// Request to authenticate user via LDAP
/// </summary>
public class LdapAuthenticationRequest
{
    [Required(ErrorMessage = "Username is required")]
    [StringLength(255, MinimumLength = 1, ErrorMessage = "Username must be between 1 and 255 characters")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required")]
    public string Password { get; set; } = string.Empty;

    public Guid? ConfigId { get; set; }
}

/// <summary>
/// LDAP authentication response with JWT tokens
/// </summary>
public class LdapAuthenticationResponse
{
    public bool Success { get; set; }
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public Guid? UserId { get; set; }
    public string? Email { get; set; }
    public string? Message { get; set; }
    public bool UserCreated { get; set; }
}

// ====================
// Test Connection
// ====================

/// <summary>
/// Request to test LDAP connection
/// </summary>
public class TestLdapConnectionRequest
{
    public Guid? ConfigId { get; set; }

    [StringLength(500)]
    public string? ServerUrl { get; set; }

    [Range(1, 65535)]
    public int? Port { get; set; }

    public bool? UseSsl { get; set; }
    public bool? UseTls { get; set; }

    [StringLength(500)]
    public string? BaseDn { get; set; }

    [StringLength(500)]
    public string? BindDn { get; set; }

    public string? BindPassword { get; set; }
}

/// <summary>
/// LDAP connection test response
/// </summary>
public class TestLdapConnectionResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? ServerInfo { get; set; }
    public int ResponseTimeMs { get; set; }
}

// ====================
// Group Sync
// ====================

/// <summary>
/// Request to trigger group synchronization
/// </summary>
public class SyncLdapGroupsRequest
{
    [Required(ErrorMessage = "Configuration ID is required")]
    public Guid ConfigId { get; set; }

    public bool ForceFullSync { get; set; } = false;
}

/// <summary>
/// Group synchronization response
/// </summary>
public class SyncLdapGroupsResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int GroupsFound { get; set; }
    public int RolesCreated { get; set; }
    public int RolesUpdated { get; set; }
    public int UsersUpdated { get; set; }
    public DateTime SyncStartedAt { get; set; }
    public DateTime SyncCompletedAt { get; set; }
    public List<string> Errors { get; set; } = new();
}

// ====================
// User Search
// ====================

/// <summary>
/// Request to search for users in LDAP
/// </summary>
public class SearchLdapUsersRequest
{
    [Required(ErrorMessage = "Configuration ID is required")]
    public Guid ConfigId { get; set; }

    [Required(ErrorMessage = "Search term is required")]
    [StringLength(255, MinimumLength = 1, ErrorMessage = "Search term must be between 1 and 255 characters")]
    public string SearchTerm { get; set; } = string.Empty;

    [Range(1, 1000, ErrorMessage = "Max results must be between 1 and 1000")]
    public int MaxResults { get; set; } = 50;
}

/// <summary>
/// LDAP user entry
/// </summary>
public class LdapUserEntry
{
    public string DistinguishedName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public List<string> Groups { get; set; } = new();
}

/// <summary>
/// Search users response
/// </summary>
public class SearchLdapUsersResponse
{
    public List<LdapUserEntry> Users { get; set; } = new();
    public int Total { get; set; }
}
