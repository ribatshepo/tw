using System.DirectoryServices.Protocols;
using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using USP.Core.Models.DTOs.Ldap;
using USP.Core.Models.Entities;
using USP.Core.Services.Authentication;
using USP.Core.Services.Cryptography;
using USP.Infrastructure.Data;

namespace USP.Infrastructure.Services.Authentication;

/// <summary>
/// Service for LDAP/Active Directory integration
/// </summary>
public class LdapService : ILdapService
{
    private readonly ApplicationDbContext _context;
    private readonly IEncryptionService _encryptionService;
    private readonly IJwtService _jwtService;
    private readonly ILogger<LdapService> _logger;

    public LdapService(
        ApplicationDbContext context,
        IEncryptionService encryptionService,
        IJwtService jwtService,
        ILogger<LdapService> logger)
    {
        _context = context;
        _encryptionService = encryptionService;
        _jwtService = jwtService;
        _logger = logger;
    }

    // ====================
    // Configuration Management
    // ====================

    public async Task<LdapConfigResponse> ConfigureLdapAsync(ConfigureLdapRequest request, Guid userId)
    {
        _logger.LogInformation("Configuring LDAP server: {Name}", request.Name);

        // Check for duplicate name
        if (await _context.Set<LdapConfiguration>().AnyAsync(c => c.Name == request.Name))
            throw new InvalidOperationException($"LDAP configuration with name '{request.Name}' already exists");

        // Encrypt bind password
        var encryptedPassword = _encryptionService.Encrypt(request.BindPassword);

        var config = new LdapConfiguration
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            ServerUrl = request.ServerUrl,
            Port = request.Port,
            UseSsl = request.UseSsl,
            UseTls = request.UseTls,
            BaseDn = request.BaseDn,
            BindDn = request.BindDn,
            BindPassword = encryptedPassword,
            UserSearchFilter = request.UserSearchFilter,
            UserSearchBase = request.UserSearchBase ?? request.BaseDn,
            GroupSearchFilter = request.GroupSearchFilter,
            GroupSearchBase = request.GroupSearchBase ?? request.BaseDn,
            EmailAttribute = request.EmailAttribute,
            FirstNameAttribute = request.FirstNameAttribute,
            LastNameAttribute = request.LastNameAttribute,
            UsernameAttribute = request.UsernameAttribute,
            GroupMembershipAttribute = request.GroupMembershipAttribute,
            EnableJitProvisioning = request.EnableJitProvisioning,
            DefaultRoleId = request.DefaultRoleId,
            SyncGroupsAsRoles = request.SyncGroupsAsRoles,
            UpdateUserOnLogin = request.UpdateUserOnLogin,
            EnableGroupSync = request.EnableGroupSync,
            GroupSyncIntervalMinutes = request.GroupSyncIntervalMinutes,
            NestedGroupsEnabled = request.NestedGroupsEnabled,
            GroupRoleMapping = request.GroupRoleMapping != null
                ? JsonSerializer.Serialize(request.GroupRoleMapping)
                : null,
            IsActive = true,
            CreatedBy = userId,
            CreatedAt = DateTime.UtcNow
        };

        _context.Set<LdapConfiguration>().Add(config);
        await _context.SaveChangesAsync();

        _logger.LogInformation("LDAP configuration created: {ConfigId}", config.Id);

        return MapToResponse(config);
    }

    public async Task<LdapConfigResponse> GetConfigurationAsync(Guid configId)
    {
        var config = await _context.Set<LdapConfiguration>()
            .FirstOrDefaultAsync(c => c.Id == configId);

        if (config == null)
            throw new InvalidOperationException($"LDAP configuration not found: {configId}");

        return MapToResponse(config);
    }

    public async Task<ListLdapConfigsResponse> ListConfigurationsAsync(bool activeOnly = true)
    {
        var query = _context.Set<LdapConfiguration>().AsQueryable();

        if (activeOnly)
            query = query.Where(c => c.IsActive);

        var configs = await query
            .OrderBy(c => c.Name)
            .ToListAsync();

        return new ListLdapConfigsResponse
        {
            Configurations = configs.Select(MapToResponse).ToList(),
            Total = configs.Count
        };
    }

    public async Task<LdapConfigResponse> UpdateConfigurationAsync(Guid configId, UpdateLdapConfigRequest request)
    {
        var config = await _context.Set<LdapConfiguration>()
            .FirstOrDefaultAsync(c => c.Id == configId);

        if (config == null)
            throw new InvalidOperationException($"LDAP configuration not found: {configId}");

        // Update fields if provided
        if (request.Name != null) config.Name = request.Name;
        if (request.ServerUrl != null) config.ServerUrl = request.ServerUrl;
        if (request.Port.HasValue) config.Port = request.Port.Value;
        if (request.UseSsl.HasValue) config.UseSsl = request.UseSsl.Value;
        if (request.UseTls.HasValue) config.UseTls = request.UseTls.Value;
        if (request.BaseDn != null) config.BaseDn = request.BaseDn;
        if (request.BindDn != null) config.BindDn = request.BindDn;
        if (request.BindPassword != null)
            config.BindPassword = _encryptionService.Encrypt(request.BindPassword);
        if (request.UserSearchFilter != null) config.UserSearchFilter = request.UserSearchFilter;
        if (request.UserSearchBase != null) config.UserSearchBase = request.UserSearchBase;
        if (request.GroupSearchFilter != null) config.GroupSearchFilter = request.GroupSearchFilter;
        if (request.GroupSearchBase != null) config.GroupSearchBase = request.GroupSearchBase;
        if (request.EmailAttribute != null) config.EmailAttribute = request.EmailAttribute;
        if (request.FirstNameAttribute != null) config.FirstNameAttribute = request.FirstNameAttribute;
        if (request.LastNameAttribute != null) config.LastNameAttribute = request.LastNameAttribute;
        if (request.UsernameAttribute != null) config.UsernameAttribute = request.UsernameAttribute;
        if (request.GroupMembershipAttribute != null)
            config.GroupMembershipAttribute = request.GroupMembershipAttribute;
        if (request.EnableJitProvisioning.HasValue)
            config.EnableJitProvisioning = request.EnableJitProvisioning.Value;
        if (request.DefaultRoleId.HasValue) config.DefaultRoleId = request.DefaultRoleId;
        if (request.SyncGroupsAsRoles.HasValue)
            config.SyncGroupsAsRoles = request.SyncGroupsAsRoles.Value;
        if (request.UpdateUserOnLogin.HasValue)
            config.UpdateUserOnLogin = request.UpdateUserOnLogin.Value;
        if (request.EnableGroupSync.HasValue)
            config.EnableGroupSync = request.EnableGroupSync.Value;
        if (request.GroupSyncIntervalMinutes.HasValue)
            config.GroupSyncIntervalMinutes = request.GroupSyncIntervalMinutes.Value;
        if (request.NestedGroupsEnabled.HasValue)
            config.NestedGroupsEnabled = request.NestedGroupsEnabled.Value;
        if (request.GroupRoleMapping != null)
            config.GroupRoleMapping = JsonSerializer.Serialize(request.GroupRoleMapping);
        if (request.IsActive.HasValue) config.IsActive = request.IsActive.Value;

        config.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("LDAP configuration updated: {ConfigId}", configId);

        return MapToResponse(config);
    }

    public async Task DeleteConfigurationAsync(Guid configId)
    {
        var config = await _context.Set<LdapConfiguration>()
            .FirstOrDefaultAsync(c => c.Id == configId);

        if (config == null)
            throw new InvalidOperationException($"LDAP configuration not found: {configId}");

        _context.Set<LdapConfiguration>().Remove(config);
        await _context.SaveChangesAsync();

        _logger.LogInformation("LDAP configuration deleted: {ConfigId}", configId);
    }

    // ====================
    // Connection Testing
    // ====================

    public async Task<TestLdapConnectionResponse> TestConnectionAsync(TestLdapConnectionRequest request)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            LdapConfiguration? config = null;

            // Use existing config or provided connection details
            if (request.ConfigId.HasValue)
            {
                config = await _context.Set<LdapConfiguration>()
                    .FirstOrDefaultAsync(c => c.Id == request.ConfigId.Value);

                if (config == null)
                    return new TestLdapConnectionResponse
                    {
                        Success = false,
                        Message = "Configuration not found"
                    };
            }

            var serverUrl = request.ServerUrl ?? config?.ServerUrl ?? throw new ArgumentException("ServerUrl required");
            var port = request.Port ?? config?.Port ?? 389;
            var useSsl = request.UseSsl ?? config?.UseSsl ?? false;
            var useTls = request.UseTls ?? config?.UseTls ?? false;
            var bindDn = request.BindDn ?? config?.BindDn ?? throw new ArgumentException("BindDn required");
            var bindPassword = request.BindPassword ??
                (config != null ? _encryptionService.Decrypt(config.BindPassword) :
                throw new ArgumentException("BindPassword required"));

            using var connection = CreateLdapConnection(serverUrl, port, useSsl, useTls);

            // Bind to test authentication
            var credential = new NetworkCredential(bindDn, bindPassword);
            connection.Bind(credential);

            var responseTime = (DateTime.UtcNow - startTime).TotalMilliseconds;

            // Update config test result if using stored config
            if (config != null)
            {
                config.LastTestResult = "Success";
                config.LastTestedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }

            _logger.LogInformation("LDAP connection test successful: {Server}:{Port}", serverUrl, port);

            return new TestLdapConnectionResponse
            {
                Success = true,
                Message = "Connection successful",
                ServerInfo = $"{serverUrl}:{port}",
                ResponseTimeMs = (int)responseTime
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LDAP connection test failed");

            // Update config test result if using stored config
            if (request.ConfigId.HasValue)
            {
                var config = await _context.Set<LdapConfiguration>()
                    .FirstOrDefaultAsync(c => c.Id == request.ConfigId.Value);

                if (config != null)
                {
                    config.LastTestResult = $"Failed: {ex.Message}";
                    config.LastTestedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }
            }

            return new TestLdapConnectionResponse
            {
                Success = false,
                Message = $"Connection failed: {ex.Message}",
                ResponseTimeMs = (int)(DateTime.UtcNow - startTime).TotalMilliseconds
            };
        }
    }

    // ====================
    // Authentication
    // ====================

    public async Task<LdapAuthenticationResponse> AuthenticateAsync(LdapAuthenticationRequest request)
    {
        _logger.LogInformation("LDAP authentication attempt for user: {Username}", request.Username);

        try
        {
            // Get LDAP configuration
            var config = request.ConfigId.HasValue
                ? await _context.Set<LdapConfiguration>().FirstOrDefaultAsync(c => c.Id == request.ConfigId.Value)
                : await _context.Set<LdapConfiguration>().FirstOrDefaultAsync(c => c.IsActive);

            if (config == null)
                throw new InvalidOperationException("No active LDAP configuration found");

            // Decrypt bind password
            var bindPassword = _encryptionService.Decrypt(config.BindPassword);

            using var connection = CreateLdapConnection(
                config.ServerUrl, config.Port, config.UseSsl, config.UseTls);

            // Bind with service account
            var serviceCredential = new NetworkCredential(config.BindDn, bindPassword);
            connection.Bind(serviceCredential);

            // Search for user
            var userDn = await SearchForUserDnAsync(connection, config, request.Username);

            if (string.IsNullOrEmpty(userDn))
            {
                _logger.LogWarning("LDAP user not found: {Username}", request.Username);
                return new LdapAuthenticationResponse
                {
                    Success = false,
                    Message = "Invalid username or password"
                };
            }

            // Authenticate user by binding with their credentials
            try
            {
                using var userConnection = CreateLdapConnection(
                    config.ServerUrl, config.Port, config.UseSsl, config.UseTls);

                var userCredential = new NetworkCredential(userDn, request.Password);
                userConnection.Bind(userCredential);

                _logger.LogInformation("LDAP authentication successful for: {Username}", request.Username);
            }
            catch (LdapException)
            {
                _logger.LogWarning("LDAP authentication failed for: {Username}", request.Username);
                return new LdapAuthenticationResponse
                {
                    Success = false,
                    Message = "Invalid username or password"
                };
            }

            // Get user details
            var userDetails = await GetUserDetailsInternalAsync(connection, config, userDn);

            if (userDetails == null)
                throw new InvalidOperationException("Failed to retrieve user details");

            // Get or create local user
            var (user, userCreated) = await GetOrCreateUserAsync(config, userDetails);

            // Update user roles from LDAP groups
            if (config.SyncGroupsAsRoles && userDetails.Groups.Any())
            {
                await UpdateUserRolesFromLdapAsync(user.Id, config.Id, userDetails.Groups);
            }

            // Generate JWT tokens
            var userRoles = await _context.Set<UserRole>()
                .Include(ur => ur.Role)
                .Where(ur => ur.UserId == user.Id)
                .Select(ur => ur.Role.Name)
                .ToListAsync();

            var accessToken = _jwtService.GenerateAccessToken(user, userRoles);
            var refreshToken = _jwtService.GenerateRefreshToken();

            _logger.LogInformation("LDAP authentication completed for user: {UserId}", user.Id);

            return new LdapAuthenticationResponse
            {
                Success = true,
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                UserId = user.Id,
                Email = user.Email,
                Message = "Authentication successful",
                UserCreated = userCreated
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LDAP authentication error for user: {Username}", request.Username);
            return new LdapAuthenticationResponse
            {
                Success = false,
                Message = "Authentication failed"
            };
        }
    }

    // ====================
    // User Operations
    // ====================

    public async Task<SearchLdapUsersResponse> SearchUsersAsync(SearchLdapUsersRequest request)
    {
        var config = await _context.Set<LdapConfiguration>()
            .FirstOrDefaultAsync(c => c.Id == request.ConfigId);

        if (config == null)
            throw new InvalidOperationException("LDAP configuration not found");

        var bindPassword = _encryptionService.Decrypt(config.BindPassword);

        using var connection = CreateLdapConnection(
            config.ServerUrl, config.Port, config.UseSsl, config.UseTls);

        var credential = new NetworkCredential(config.BindDn, bindPassword);
        connection.Bind(credential);

        // Build search filter
        var filter = $"(&{config.UserSearchFilter.Replace("{0}", request.SearchTerm)}(objectClass=user))";

        var searchRequest = new SearchRequest(
            config.UserSearchBase ?? config.BaseDn,
            filter,
            SearchScope.Subtree,
            config.EmailAttribute,
            config.FirstNameAttribute,
            config.LastNameAttribute,
            config.UsernameAttribute,
            config.GroupMembershipAttribute
        );

        var searchResponse = (SearchResponse)connection.SendRequest(searchRequest);

        var users = new List<LdapUserEntry>();

        foreach (SearchResultEntry entry in searchResponse.Entries)
        {
            var user = new LdapUserEntry
            {
                DistinguishedName = entry.DistinguishedName,
                Username = GetAttributeValue(entry, config.UsernameAttribute) ?? string.Empty,
                Email = GetAttributeValue(entry, config.EmailAttribute) ?? string.Empty,
                FirstName = GetAttributeValue(entry, config.FirstNameAttribute),
                LastName = GetAttributeValue(entry, config.LastNameAttribute),
                Groups = GetAttributeValues(entry, config.GroupMembershipAttribute)
            };

            users.Add(user);

            if (users.Count >= request.MaxResults)
                break;
        }

        return new SearchLdapUsersResponse
        {
            Users = users,
            Total = users.Count
        };
    }

    public async Task<LdapUserEntry?> GetUserDetailsAsync(Guid configId, string username)
    {
        var config = await _context.Set<LdapConfiguration>()
            .FirstOrDefaultAsync(c => c.Id == configId);

        if (config == null)
            throw new InvalidOperationException("LDAP configuration not found");

        var bindPassword = _encryptionService.Decrypt(config.BindPassword);

        using var connection = CreateLdapConnection(
            config.ServerUrl, config.Port, config.UseSsl, config.UseTls);

        var credential = new NetworkCredential(config.BindDn, bindPassword);
        connection.Bind(credential);

        var userDn = await SearchForUserDnAsync(connection, config, username);

        if (string.IsNullOrEmpty(userDn))
            return null;

        return await GetUserDetailsInternalAsync(connection, config, userDn);
    }

    // ====================
    // Group Synchronization
    // ====================

    public async Task<SyncLdapGroupsResponse> SyncGroupsAsync(SyncLdapGroupsRequest request)
    {
        var syncStarted = DateTime.UtcNow;
        var errors = new List<string>();
        var rolesCreated = 0;
        var rolesUpdated = 0;
        var usersUpdated = 0;

        try
        {
            var config = await _context.Set<LdapConfiguration>()
                .FirstOrDefaultAsync(c => c.Id == request.ConfigId);

            if (config == null)
                throw new InvalidOperationException("LDAP configuration not found");

            if (!config.EnableGroupSync)
                throw new InvalidOperationException("Group sync is not enabled for this configuration");

            var bindPassword = _encryptionService.Decrypt(config.BindPassword);

            using var connection = CreateLdapConnection(
                config.ServerUrl, config.Port, config.UseSsl, config.UseTls);

            var credential = new NetworkCredential(config.BindDn, bindPassword);
            connection.Bind(credential);

            // Search for groups
            var filter = config.GroupSearchFilter ?? "(objectClass=group)";

            var searchRequest = new SearchRequest(
                config.GroupSearchBase ?? config.BaseDn,
                filter,
                SearchScope.Subtree,
                "cn", "distinguishedName", "member"
            );

            var searchResponse = (SearchResponse)connection.SendRequest(searchRequest);

            var groupsFound = searchResponse.Entries.Count;

            _logger.LogInformation("Found {Count} LDAP groups", groupsFound);

            // Process groups
            foreach (SearchResultEntry entry in searchResponse.Entries)
            {
                try
                {
                    var groupName = GetAttributeValue(entry, "cn");
                    var groupDn = entry.DistinguishedName;

                    if (string.IsNullOrEmpty(groupName))
                        continue;

                    // Check if role exists
                    var role = await _context.Set<Role>()
                        .FirstOrDefaultAsync(r => r.Name == groupName);

                    if (role == null)
                    {
                        // Create role
                        role = new Role
                        {
                            Id = Guid.NewGuid(),
                            Name = groupName,
                            Description = $"LDAP Group: {groupDn}",
                            IsBuiltIn = false,
                            CreatedAt = DateTime.UtcNow
                        };

                        _context.Set<Role>().Add(role);
                        await _context.SaveChangesAsync();

                        rolesCreated++;
                        _logger.LogInformation("Created role from LDAP group: {GroupName}", groupName);
                    }
                    else
                    {
                        role.UpdatedAt = DateTime.UtcNow;
                        rolesUpdated++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing LDAP group");
                    errors.Add($"Group error: {ex.Message}");
                }
            }

            await _context.SaveChangesAsync();

            // Update last sync time
            config.LastGroupSync = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            var syncCompleted = DateTime.UtcNow;

            return new SyncLdapGroupsResponse
            {
                Success = true,
                Message = "Group synchronization completed successfully",
                GroupsFound = groupsFound,
                RolesCreated = rolesCreated,
                RolesUpdated = rolesUpdated,
                UsersUpdated = usersUpdated,
                SyncStartedAt = syncStarted,
                SyncCompletedAt = syncCompleted,
                Errors = errors
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LDAP group sync failed");

            return new SyncLdapGroupsResponse
            {
                Success = false,
                Message = $"Group synchronization failed: {ex.Message}",
                SyncStartedAt = syncStarted,
                SyncCompletedAt = DateTime.UtcNow,
                Errors = errors
            };
        }
    }

    public async Task UpdateUserRolesFromLdapAsync(Guid userId, Guid configId, List<string> ldapGroups)
    {
        var config = await _context.Set<LdapConfiguration>()
            .FirstOrDefaultAsync(c => c.Id == configId);

        if (config == null || !config.SyncGroupsAsRoles)
            return;

        // Parse group role mapping
        Dictionary<string, string>? groupRoleMapping = null;
        if (!string.IsNullOrEmpty(config.GroupRoleMapping))
        {
            groupRoleMapping = JsonSerializer.Deserialize<Dictionary<string, string>>(config.GroupRoleMapping);
        }

        // Extract group names from DNs
        var groupNames = ldapGroups
            .Select(ExtractCnFromDn)
            .Where(cn => !string.IsNullOrEmpty(cn))
            .ToList();

        // If mapping exists, map LDAP groups to role names
        if (groupRoleMapping != null)
        {
            groupNames = ldapGroups
                .Where(g => groupRoleMapping.ContainsKey(g))
                .Select(g => groupRoleMapping[g])
                .ToList();
        }

        // Get roles matching group names
        var roles = await _context.Set<Role>()
            .Where(r => groupNames.Contains(r.Name))
            .ToListAsync();

        // Remove existing user roles
        var existingUserRoles = await _context.Set<UserRole>()
            .Where(ur => ur.UserId == userId)
            .ToListAsync();

        _context.Set<UserRole>().RemoveRange(existingUserRoles);

        // Add new user roles
        foreach (var role in roles)
        {
            var userRole = new UserRole
            {
                UserId = userId,
                RoleId = role.Id,
                AssignedAt = DateTime.UtcNow
            };

            _context.Set<UserRole>().Add(userRole);
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Updated roles for user {UserId} from LDAP groups", userId);
    }

    // ====================
    // Helper Methods
    // ====================

    private LdapConnection CreateLdapConnection(string serverUrl, int port, bool useSsl, bool useTls)
    {
        var identifier = new LdapDirectoryIdentifier(serverUrl, port, false, false);
        var connection = new LdapConnection(identifier)
        {
            AuthType = AuthType.Basic,
            Timeout = TimeSpan.FromSeconds(30)
        };

        if (useSsl)
        {
            connection.SessionOptions.SecureSocketLayer = true;
        }

        if (useTls)
        {
            connection.SessionOptions.StartTransportLayerSecurity(null);
        }

        return connection;
    }

    private async Task<string?> SearchForUserDnAsync(LdapConnection connection, LdapConfiguration config, string username)
    {
        var filter = config.UserSearchFilter.Replace("{0}", username);

        var searchRequest = new SearchRequest(
            config.UserSearchBase ?? config.BaseDn,
            filter,
            SearchScope.Subtree,
            null
        );

        var searchResponse = (SearchResponse)connection.SendRequest(searchRequest);

        return searchResponse.Entries.Count > 0
            ? searchResponse.Entries[0].DistinguishedName
            : null;
    }

    private async Task<LdapUserEntry?> GetUserDetailsInternalAsync(
        LdapConnection connection, LdapConfiguration config, string userDn)
    {
        var searchRequest = new SearchRequest(
            userDn,
            "(objectClass=*)",
            SearchScope.Base,
            config.EmailAttribute,
            config.FirstNameAttribute,
            config.LastNameAttribute,
            config.UsernameAttribute,
            config.GroupMembershipAttribute
        );

        var searchResponse = (SearchResponse)connection.SendRequest(searchRequest);

        if (searchResponse.Entries.Count == 0)
            return null;

        var entry = searchResponse.Entries[0];

        return new LdapUserEntry
        {
            DistinguishedName = entry.DistinguishedName,
            Username = GetAttributeValue(entry, config.UsernameAttribute) ?? string.Empty,
            Email = GetAttributeValue(entry, config.EmailAttribute) ?? string.Empty,
            FirstName = GetAttributeValue(entry, config.FirstNameAttribute),
            LastName = GetAttributeValue(entry, config.LastNameAttribute),
            Groups = GetAttributeValues(entry, config.GroupMembershipAttribute)
        };
    }

    private async Task<(ApplicationUser user, bool userCreated)> GetOrCreateUserAsync(
        LdapConfiguration config, LdapUserEntry userDetails)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == userDetails.Email);

        if (user != null)
        {
            // Update user if configured
            if (config.UpdateUserOnLogin)
            {
                user.FirstName = userDetails.FirstName ?? user.FirstName;
                user.LastName = userDetails.LastName ?? user.LastName;
                await _context.SaveChangesAsync();
            }

            return (user, false);
        }

        // JIT provisioning
        if (!config.EnableJitProvisioning)
            throw new InvalidOperationException($"User {userDetails.Email} does not exist and JIT provisioning is disabled");

        user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = userDetails.Email,
            NormalizedEmail = userDetails.Email.ToUpperInvariant(),
            UserName = userDetails.Username,
            NormalizedUserName = userDetails.Username.ToUpperInvariant(),
            EmailConfirmed = true,
            FirstName = userDetails.FirstName ?? string.Empty,
            LastName = userDetails.LastName ?? string.Empty,
            Status = "active",
            CreatedAt = DateTime.UtcNow,
            SecurityStamp = Guid.NewGuid().ToString()
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Assign default role if specified
        if (config.DefaultRoleId.HasValue)
        {
            var userRole = new UserRole
            {
                UserId = user.Id,
                RoleId = config.DefaultRoleId.Value,
                AssignedAt = DateTime.UtcNow
            };

            _context.Set<UserRole>().Add(userRole);
            await _context.SaveChangesAsync();
        }

        _logger.LogInformation("JIT provisioned user from LDAP: {Email}", userDetails.Email);

        return (user, true);
    }

    private string? GetAttributeValue(SearchResultEntry entry, string attributeName)
    {
        if (entry.Attributes.Contains(attributeName))
        {
            var attribute = entry.Attributes[attributeName];
            if (attribute.Count > 0)
                return attribute[0]?.ToString();
        }

        return null;
    }

    private List<string> GetAttributeValues(SearchResultEntry entry, string attributeName)
    {
        var values = new List<string>();

        if (entry.Attributes.Contains(attributeName))
        {
            var attribute = entry.Attributes[attributeName];
            for (int i = 0; i < attribute.Count; i++)
            {
                var value = attribute[i]?.ToString();
                if (!string.IsNullOrEmpty(value))
                    values.Add(value);
            }
        }

        return values;
    }

    private string? ExtractCnFromDn(string dn)
    {
        // Extract CN from DN (e.g., "CN=Admins,OU=Groups,DC=example,DC=com" -> "Admins")
        if (string.IsNullOrEmpty(dn))
            return null;

        var match = System.Text.RegularExpressions.Regex.Match(dn, @"^CN=([^,]+)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        return match.Success ? match.Groups[1].Value : null;
    }

    private LdapConfigResponse MapToResponse(LdapConfiguration config)
    {
        Dictionary<string, string>? groupRoleMapping = null;
        if (!string.IsNullOrEmpty(config.GroupRoleMapping))
        {
            groupRoleMapping = JsonSerializer.Deserialize<Dictionary<string, string>>(config.GroupRoleMapping);
        }

        return new LdapConfigResponse
        {
            Id = config.Id,
            Name = config.Name,
            ServerUrl = config.ServerUrl,
            Port = config.Port,
            UseSsl = config.UseSsl,
            UseTls = config.UseTls,
            BaseDn = config.BaseDn,
            BindDn = config.BindDn,
            UserSearchFilter = config.UserSearchFilter,
            UserSearchBase = config.UserSearchBase,
            GroupSearchFilter = config.GroupSearchFilter,
            GroupSearchBase = config.GroupSearchBase,
            EmailAttribute = config.EmailAttribute,
            FirstNameAttribute = config.FirstNameAttribute,
            LastNameAttribute = config.LastNameAttribute,
            UsernameAttribute = config.UsernameAttribute,
            GroupMembershipAttribute = config.GroupMembershipAttribute,
            EnableJitProvisioning = config.EnableJitProvisioning,
            DefaultRoleId = config.DefaultRoleId,
            SyncGroupsAsRoles = config.SyncGroupsAsRoles,
            UpdateUserOnLogin = config.UpdateUserOnLogin,
            EnableGroupSync = config.EnableGroupSync,
            GroupSyncIntervalMinutes = config.GroupSyncIntervalMinutes,
            LastGroupSync = config.LastGroupSync,
            NestedGroupsEnabled = config.NestedGroupsEnabled,
            GroupRoleMapping = groupRoleMapping,
            IsActive = config.IsActive,
            LastTestResult = config.LastTestResult,
            LastTestedAt = config.LastTestedAt,
            CreatedAt = config.CreatedAt,
            UpdatedAt = config.UpdatedAt
        };
    }
}
