using USP.Core.Models.DTOs.Ldap;

namespace USP.Core.Services.Authentication;

/// <summary>
/// Service for LDAP/Active Directory integration
/// </summary>
public interface ILdapService
{
    // ====================
    // Configuration Management
    // ====================

    /// <summary>
    /// Configure LDAP server
    /// </summary>
    Task<LdapConfigResponse> ConfigureLdapAsync(ConfigureLdapRequest request, Guid userId);

    /// <summary>
    /// Get LDAP configuration by ID
    /// </summary>
    Task<LdapConfigResponse> GetConfigurationAsync(Guid configId);

    /// <summary>
    /// List all LDAP configurations
    /// </summary>
    Task<ListLdapConfigsResponse> ListConfigurationsAsync(bool activeOnly = true);

    /// <summary>
    /// Update LDAP configuration
    /// </summary>
    Task<LdapConfigResponse> UpdateConfigurationAsync(Guid configId, UpdateLdapConfigRequest request);

    /// <summary>
    /// Delete LDAP configuration
    /// </summary>
    Task DeleteConfigurationAsync(Guid configId);

    // ====================
    // Connection Testing
    // ====================

    /// <summary>
    /// Test LDAP connection
    /// </summary>
    Task<TestLdapConnectionResponse> TestConnectionAsync(TestLdapConnectionRequest request);

    // ====================
    // Authentication
    // ====================

    /// <summary>
    /// Authenticate user against LDAP directory
    /// </summary>
    Task<LdapAuthenticationResponse> AuthenticateAsync(LdapAuthenticationRequest request);

    // ====================
    // User Operations
    // ====================

    /// <summary>
    /// Search for users in LDAP directory
    /// </summary>
    Task<SearchLdapUsersResponse> SearchUsersAsync(SearchLdapUsersRequest request);

    /// <summary>
    /// Get user details from LDAP
    /// </summary>
    Task<LdapUserEntry?> GetUserDetailsAsync(Guid configId, string username);

    // ====================
    // Group Synchronization
    // ====================

    /// <summary>
    /// Synchronize LDAP groups to local roles
    /// </summary>
    Task<SyncLdapGroupsResponse> SyncGroupsAsync(SyncLdapGroupsRequest request);

    /// <summary>
    /// Update user's roles based on LDAP group membership
    /// </summary>
    Task UpdateUserRolesFromLdapAsync(Guid userId, Guid configId, List<string> ldapGroups);
}
