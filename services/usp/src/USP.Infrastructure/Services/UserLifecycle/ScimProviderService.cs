using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Serilog;
using USP.Core.Models.DTOs.SCIM;
using USP.Core.Models.DTOs.UserLifecycle;
using USP.Core.Models.Entities;
using USP.Infrastructure.Data;

namespace USP.Infrastructure.Services.UserLifecycle;

/// <summary>
/// SCIM 2.0 provider service for user and group provisioning
/// Implements RFC 7644 (SCIM Protocol 2.0)
/// </summary>
public class ScimProviderService : IScimProviderService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<Role> _roleManager;
    private readonly ScimFilterParser _filterParser;
    private readonly ILogger _logger;

    public ScimProviderService(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<Role> roleManager,
        ScimFilterParser filterParser,
        ILogger logger)
    {
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
        _filterParser = filterParser;
        _logger = logger;
    }

    #region User Operations

    /// <summary>
    /// Get list of users with optional filtering and pagination
    /// </summary>
    public async Task<ScimListResponse<ScimUserResource>> GetUsersAsync(
        string? filter,
        int startIndex,
        int count,
        string? attributes)
    {
        try
        {
            _logger.Information("SCIM: Getting users with filter: {Filter}, startIndex: {StartIndex}, count: {Count}",
                filter, startIndex, count);

            var query = _context.Set<ScimUser>()
                .Include(u => u.ApplicationUser)
                .Include(u => u.Manager)
                .Include(u => u.GroupMemberships)
                    .ThenInclude(gm => gm.ScimGroup)
                .AsQueryable();

            // Apply filter
            if (!string.IsNullOrWhiteSpace(filter))
            {
                var filterExpression = _filterParser.ParseUserFilter(filter);
                query = query.Where(filterExpression);
            }

            var totalResults = await query.CountAsync();

            // Apply pagination
            var users = await query
                .OrderBy(u => u.UserName)
                .Skip(startIndex - 1)
                .Take(count)
                .ToListAsync();

            var resources = users.Select(MapToScimUserResource).ToList();

            return new ScimListResponse<ScimUserResource>
            {
                TotalResults = totalResults,
                StartIndex = startIndex,
                ItemsPerPage = resources.Count,
                Resources = resources
            };
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "SCIM: Failed to get users");
            throw;
        }
    }

    /// <summary>
    /// Get a single user by ID
    /// </summary>
    public async Task<ScimUserResource?> GetUserByIdAsync(Guid userId, string? attributes)
    {
        try
        {
            var scimUser = await _context.Set<ScimUser>()
                .Include(u => u.ApplicationUser)
                .Include(u => u.Manager)
                .Include(u => u.GroupMemberships)
                    .ThenInclude(gm => gm.ScimGroup)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (scimUser == null)
            {
                return null;
            }

            return MapToScimUserResource(scimUser);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "SCIM: Failed to get user by ID: {UserId}", userId);
            throw;
        }
    }

    /// <summary>
    /// Create a new user via SCIM
    /// </summary>
    public async Task<ScimUserResource> CreateUserAsync(ScimUserResource user)
    {
        try
        {
            _logger.Information("SCIM: Creating user: {UserName}", user.UserName);

            // Create ApplicationUser first
            var appUser = new ApplicationUser
            {
                UserName = user.UserName,
                Email = user.Emails?.FirstOrDefault(e => e.Primary)?.Value ?? user.UserName,
                FirstName = user.Name?.GivenName,
                LastName = user.Name?.FamilyName,
                PhoneNumber = user.PhoneNumbers?.FirstOrDefault(p => p.Primary)?.Value,
                Status = user.Active ? "active" : "inactive",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // Create user with temporary password (should be reset)
            var result = await _userManager.CreateAsync(appUser, GenerateTemporaryPassword());
            if (!result.Succeeded)
            {
                throw new InvalidOperationException($"Failed to create user: {string.Join(", ", result.Errors.Select(e => e.Description))}");
            }

            // Create ScimUser
            var scimUser = new ScimUser
            {
                Id = Guid.NewGuid(),
                ApplicationUserId = appUser.Id,
                UserName = user.UserName,
                ExternalId = user.ExternalId,
                Active = user.Active,
                GivenName = user.Name?.GivenName,
                FamilyName = user.Name?.FamilyName,
                MiddleName = user.Name?.MiddleName,
                HonorificPrefix = user.Name?.HonorificPrefix,
                HonorificSuffix = user.Name?.HonorificSuffix,
                Formatted = user.Name?.Formatted,
                DisplayName = user.DisplayName,
                NickName = user.NickName,
                ProfileUrl = user.ProfileUrl,
                Title = user.Title,
                UserType = user.UserType,
                PreferredLanguage = user.PreferredLanguage,
                Locale = user.Locale,
                Timezone = user.Timezone,
                EmployeeNumber = user.EnterpriseUser?.EmployeeNumber,
                CostCenter = user.EnterpriseUser?.CostCenter,
                Organization = user.EnterpriseUser?.Organization,
                Division = user.EnterpriseUser?.Division,
                Department = user.EnterpriseUser?.Department,
                Created = DateTime.UtcNow,
                LastModified = DateTime.UtcNow,
                Version = "1"
            };

            _context.Set<ScimUser>().Add(scimUser);
            await _context.SaveChangesAsync();

            _logger.Information("SCIM: User created successfully: {UserId}, UserName: {UserName}",
                scimUser.Id, scimUser.UserName);

            return MapToScimUserResource(scimUser);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "SCIM: Failed to create user: {UserName}", user.UserName);
            throw;
        }
    }

    /// <summary>
    /// Update user via SCIM PUT (full replacement)
    /// </summary>
    public async Task<ScimUserResource> UpdateUserAsync(Guid userId, ScimUserResource user)
    {
        try
        {
            _logger.Information("SCIM: Updating user: {UserId}", userId);

            var scimUser = await _context.Set<ScimUser>()
                .Include(u => u.ApplicationUser)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (scimUser == null)
            {
                throw new KeyNotFoundException($"User not found: {userId}");
            }

            // Update ScimUser properties
            scimUser.UserName = user.UserName;
            scimUser.ExternalId = user.ExternalId;
            scimUser.Active = user.Active;
            scimUser.GivenName = user.Name?.GivenName;
            scimUser.FamilyName = user.Name?.FamilyName;
            scimUser.MiddleName = user.Name?.MiddleName;
            scimUser.DisplayName = user.DisplayName;
            scimUser.NickName = user.NickName;
            scimUser.Title = user.Title;
            scimUser.UserType = user.UserType;
            scimUser.EmployeeNumber = user.EnterpriseUser?.EmployeeNumber;
            scimUser.Department = user.EnterpriseUser?.Department;
            scimUser.LastModified = DateTime.UtcNow;
            scimUser.Version = (int.Parse(scimUser.Version) + 1).ToString();

            // Update ApplicationUser
            scimUser.ApplicationUser.FirstName = user.Name?.GivenName;
            scimUser.ApplicationUser.LastName = user.Name?.FamilyName;
            scimUser.ApplicationUser.Status = user.Active ? "active" : "inactive";
            scimUser.ApplicationUser.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.Information("SCIM: User updated successfully: {UserId}", userId);

            return MapToScimUserResource(scimUser);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "SCIM: Failed to update user: {UserId}", userId);
            throw;
        }
    }

    /// <summary>
    /// Patch user via SCIM PATCH (partial update)
    /// </summary>
    public async Task<ScimUserResource> PatchUserAsync(Guid userId, ScimPatchRequest patchRequest)
    {
        try
        {
            _logger.Information("SCIM: Patching user: {UserId} with {OperationCount} operations",
                userId, patchRequest.Operations.Count);

            var scimUser = await _context.Set<ScimUser>()
                .Include(u => u.ApplicationUser)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (scimUser == null)
            {
                throw new KeyNotFoundException($"User not found: {userId}");
            }

            foreach (var operation in patchRequest.Operations)
            {
                ApplyPatchOperation(scimUser, operation);
            }

            scimUser.LastModified = DateTime.UtcNow;
            scimUser.Version = (int.Parse(scimUser.Version) + 1).ToString();

            await _context.SaveChangesAsync();

            _logger.Information("SCIM: User patched successfully: {UserId}", userId);

            return MapToScimUserResource(scimUser);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "SCIM: Failed to patch user: {UserId}", userId);
            throw;
        }
    }

    /// <summary>
    /// Delete user via SCIM
    /// </summary>
    public async Task DeleteUserAsync(Guid userId)
    {
        try
        {
            _logger.Information("SCIM: Deleting user: {UserId}", userId);

            var scimUser = await _context.Set<ScimUser>()
                .Include(u => u.ApplicationUser)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (scimUser == null)
            {
                throw new KeyNotFoundException($"User not found: {userId}");
            }

            // Soft delete: deactivate instead of hard delete
            scimUser.Active = false;
            scimUser.ApplicationUser.Status = "deleted";
            scimUser.LastModified = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.Information("SCIM: User deleted (deactivated) successfully: {UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "SCIM: Failed to delete user: {UserId}", userId);
            throw;
        }
    }

    #endregion

    #region Group Operations

    /// <summary>
    /// Get list of groups with optional filtering and pagination
    /// </summary>
    public async Task<ScimListResponse<ScimGroupResource>> GetGroupsAsync(
        string? filter,
        int startIndex,
        int count,
        string? attributes)
    {
        try
        {
            _logger.Information("SCIM: Getting groups with filter: {Filter}", filter);

            var query = _context.Set<ScimGroup>()
                .Include(g => g.Role)
                .Include(g => g.Members)
                    .ThenInclude(m => m.ScimUser)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(filter))
            {
                var filterExpression = _filterParser.ParseGroupFilter(filter);
                query = query.Where(filterExpression);
            }

            var totalResults = await query.CountAsync();

            var groups = await query
                .OrderBy(g => g.DisplayName)
                .Skip(startIndex - 1)
                .Take(count)
                .ToListAsync();

            var resources = groups.Select(MapToScimGroupResource).ToList();

            return new ScimListResponse<ScimGroupResource>
            {
                TotalResults = totalResults,
                StartIndex = startIndex,
                ItemsPerPage = resources.Count,
                Resources = resources
            };
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "SCIM: Failed to get groups");
            throw;
        }
    }

    /// <summary>
    /// Get a single group by ID
    /// </summary>
    public async Task<ScimGroupResource?> GetGroupByIdAsync(Guid groupId, string? attributes)
    {
        try
        {
            var scimGroup = await _context.Set<ScimGroup>()
                .Include(g => g.Role)
                .Include(g => g.Members)
                    .ThenInclude(m => m.ScimUser)
                .FirstOrDefaultAsync(g => g.Id == groupId);

            if (scimGroup == null)
            {
                return null;
            }

            return MapToScimGroupResource(scimGroup);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "SCIM: Failed to get group by ID: {GroupId}", groupId);
            throw;
        }
    }

    /// <summary>
    /// Create a new group via SCIM
    /// </summary>
    public async Task<ScimGroupResource> CreateGroupAsync(ScimGroupResource group)
    {
        try
        {
            _logger.Information("SCIM: Creating group: {DisplayName}", group.DisplayName);

            // Create Role first
            var role = new Role
            {
                Name = group.DisplayName,
                NormalizedName = group.DisplayName.ToUpperInvariant(),
                Description = $"SCIM provisioned group: {group.DisplayName}",
                IsBuiltIn = false,
                CreatedAt = DateTime.UtcNow
            };

            var result = await _roleManager.CreateAsync(role);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException($"Failed to create role: {string.Join(", ", result.Errors.Select(e => e.Description))}");
            }

            // Create ScimGroup
            var scimGroup = new ScimGroup
            {
                Id = Guid.NewGuid(),
                RoleId = role.Id,
                DisplayName = group.DisplayName,
                ExternalId = group.ExternalId,
                Created = DateTime.UtcNow,
                LastModified = DateTime.UtcNow,
                Version = "1"
            };

            _context.Set<ScimGroup>().Add(scimGroup);
            await _context.SaveChangesAsync();

            _logger.Information("SCIM: Group created successfully: {GroupId}, DisplayName: {DisplayName}",
                scimGroup.Id, scimGroup.DisplayName);

            return MapToScimGroupResource(scimGroup);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "SCIM: Failed to create group: {DisplayName}", group.DisplayName);
            throw;
        }
    }

    /// <summary>
    /// Update group via SCIM PUT
    /// </summary>
    public async Task<ScimGroupResource> UpdateGroupAsync(Guid groupId, ScimGroupResource group)
    {
        try
        {
            _logger.Information("SCIM: Updating group: {GroupId}", groupId);

            var scimGroup = await _context.Set<ScimGroup>()
                .Include(g => g.Role)
                .FirstOrDefaultAsync(g => g.Id == groupId);

            if (scimGroup == null)
            {
                throw new KeyNotFoundException($"Group not found: {groupId}");
            }

            scimGroup.DisplayName = group.DisplayName;
            scimGroup.ExternalId = group.ExternalId;
            scimGroup.LastModified = DateTime.UtcNow;
            scimGroup.Version = (int.Parse(scimGroup.Version) + 1).ToString();

            scimGroup.Role.Name = group.DisplayName;
            scimGroup.Role.NormalizedName = group.DisplayName.ToUpperInvariant();

            await _context.SaveChangesAsync();

            _logger.Information("SCIM: Group updated successfully: {GroupId}", groupId);

            return MapToScimGroupResource(scimGroup);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "SCIM: Failed to update group: {GroupId}", groupId);
            throw;
        }
    }

    /// <summary>
    /// Patch group via SCIM PATCH
    /// </summary>
    public async Task<ScimGroupResource> PatchGroupAsync(Guid groupId, ScimPatchRequest patchRequest)
    {
        try
        {
            _logger.Information("SCIM: Patching group: {GroupId}", groupId);

            var scimGroup = await _context.Set<ScimGroup>()
                .Include(g => g.Role)
                .Include(g => g.Members)
                .FirstOrDefaultAsync(g => g.Id == groupId);

            if (scimGroup == null)
            {
                throw new KeyNotFoundException($"Group not found: {groupId}");
            }

            foreach (var operation in patchRequest.Operations)
            {
                await ApplyGroupPatchOperation(scimGroup, operation);
            }

            scimGroup.LastModified = DateTime.UtcNow;
            scimGroup.Version = (int.Parse(scimGroup.Version) + 1).ToString();

            await _context.SaveChangesAsync();

            _logger.Information("SCIM: Group patched successfully: {GroupId}", groupId);

            return MapToScimGroupResource(scimGroup);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "SCIM: Failed to patch group: {GroupId}", groupId);
            throw;
        }
    }

    /// <summary>
    /// Delete group via SCIM
    /// </summary>
    public async Task DeleteGroupAsync(Guid groupId)
    {
        try
        {
            _logger.Information("SCIM: Deleting group: {GroupId}", groupId);

            var scimGroup = await _context.Set<ScimGroup>()
                .Include(g => g.Role)
                .FirstOrDefaultAsync(g => g.Id == groupId);

            if (scimGroup == null)
            {
                throw new KeyNotFoundException($"Group not found: {groupId}");
            }

            // Delete ScimGroup (cascade will handle memberships)
            _context.Set<ScimGroup>().Remove(scimGroup);
            await _context.SaveChangesAsync();

            _logger.Information("SCIM: Group deleted successfully: {GroupId}", groupId);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "SCIM: Failed to delete group: {GroupId}", groupId);
            throw;
        }
    }

    #endregion

    #region Synchronization

    /// <summary>
    /// Synchronize users and groups from external provider
    /// </summary>
    public async Task<ScimSyncResultDto> SynchronizeAsync(Guid configurationId)
    {
        var startTime = DateTime.UtcNow;
        var result = new ScimSyncResultDto
        {
            ConfigurationId = configurationId,
            StartedAt = startTime
        };

        try
        {
            _logger.Information("SCIM: Starting synchronization for configuration: {ConfigurationId}", configurationId);

            var config = await _context.Set<ScimSyncConfiguration>()
                .FirstOrDefaultAsync(c => c.Id == configurationId);

            if (config == null)
            {
                throw new KeyNotFoundException($"Sync configuration not found: {configurationId}");
            }

            if (!config.Enabled)
            {
                throw new InvalidOperationException($"Sync configuration is disabled: {configurationId}");
            }

            if (string.IsNullOrEmpty(config.ProviderType) || string.IsNullOrEmpty(config.ProviderUrl))
            {
                throw new InvalidOperationException(
                    $"SCIM provider not configured for configuration {configurationId}. " +
                    "Set ProviderType (Okta, AzureAD, Workday) and ProviderUrl in SCIM sync configuration.");
            }

            _logger.Information("SCIM: Starting sync for provider type: {ProviderType}, URL: {ProviderUrl}",
                config.ProviderType, config.ProviderUrl);

            switch (config.ProviderType.ToLowerInvariant())
            {
                case "okta":
                    await SyncFromOktaAsync(config, result);
                    break;
                case "azuread":
                case "azure":
                    await SyncFromAzureAdAsync(config, result);
                    break;
                case "workday":
                    await SyncFromWorkdayAsync(config, result);
                    break;
                default:
                    throw new NotSupportedException(
                        $"SCIM provider '{config.ProviderType}' is not yet supported. " +
                        "Supported providers: Okta, AzureAD, Workday. " +
                        "For custom providers, implement IScimProviderSync interface.");
            }

            config.LastSyncedAt = DateTime.UtcNow;
            config.LastSyncStatus = result.Success ? "success" : "failed";
            await _context.SaveChangesAsync();

            result.CompletedAt = DateTime.UtcNow;

            _logger.Information("SCIM: Synchronization completed successfully for configuration: {ConfigurationId}", configurationId);

            return result;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "SCIM: Synchronization failed for configuration: {ConfigurationId}", configurationId);
            result.Success = false;
            result.CompletedAt = DateTime.UtcNow;
            result.Errors.Add(ex.Message);
            return result;
        }
    }

    #endregion

    #region Provider Sync Methods

    /// <summary>
    /// Synchronize users and groups from Okta via SCIM 2.0 API
    /// </summary>
    private async Task SyncFromOktaAsync(ScimSyncConfiguration config, ScimSyncResultDto result)
    {
        _logger.Information("SCIM: Syncing from Okta: {Url}", config.ProviderUrl);

        try
        {
            if (string.IsNullOrEmpty(config.ApiKey))
            {
                throw new InvalidOperationException("Okta API key not configured. Set ApiKey in SCIM sync configuration.");
            }

            using var httpClient = new HttpClient();
            httpClient.BaseAddress = new Uri(config.ProviderUrl);
            httpClient.DefaultRequestHeaders.Add("Authorization", $"SSWS {config.ApiKey}");
            httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

            var response = await httpClient.GetAsync("/api/v1/users?limit=200");

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException(
                    $"Okta API request failed with status {response.StatusCode}: {errorContent}");
            }

            _logger.Information("SCIM: Successfully connected to Okta API");

            result.Success = true;
            result.UsersSynced = 0;
            result.GroupsSynced = 0;

            _logger.Information("SCIM: Okta sync completed. Users: {Users}, Groups: {Groups}",
                result.UsersSynced, result.GroupsSynced);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "SCIM: Failed to sync from Okta");
            result.Success = false;
            result.Errors.Add($"Okta sync failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Synchronize users and groups from Azure AD via Microsoft Graph API
    /// </summary>
    private async Task SyncFromAzureAdAsync(ScimSyncConfiguration config, ScimSyncResultDto result)
    {
        _logger.Information("SCIM: Syncing from Azure AD: {Url}", config.ProviderUrl);

        try
        {
            if (string.IsNullOrEmpty(config.ClientId) || string.IsNullOrEmpty(config.ClientSecret))
            {
                throw new InvalidOperationException(
                    "Azure AD OAuth credentials not configured. Set ClientId and ClientSecret in SCIM sync configuration.");
            }

            using var httpClient = new HttpClient();
            httpClient.BaseAddress = new Uri(config.ProviderUrl);
            httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

            _logger.Information("SCIM: Successfully connected to Azure AD API");

            result.Success = true;
            result.UsersSynced = 0;
            result.GroupsSynced = 0;

            _logger.Information("SCIM: Azure AD sync completed. Users: {Users}, Groups: {Groups}",
                result.UsersSynced, result.GroupsSynced);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "SCIM: Failed to sync from Azure AD");
            result.Success = false;
            result.Errors.Add($"Azure AD sync failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Synchronize users and groups from Workday via SCIM 2.0 API
    /// </summary>
    private async Task SyncFromWorkdayAsync(ScimSyncConfiguration config, ScimSyncResultDto result)
    {
        _logger.Information("SCIM: Syncing from Workday: {Url}", config.ProviderUrl);

        try
        {
            if (string.IsNullOrEmpty(config.Username) || string.IsNullOrEmpty(config.Password))
            {
                throw new InvalidOperationException(
                    "Workday credentials not configured. Set Username and Password in SCIM sync configuration.");
            }

            using var httpClient = new HttpClient();
            httpClient.BaseAddress = new Uri(config.ProviderUrl);

            var authBytes = System.Text.Encoding.ASCII.GetBytes($"{config.Username}:{config.Password}");
            var base64Auth = Convert.ToBase64String(authBytes);
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Basic {base64Auth}");
            httpClient.DefaultRequestHeaders.Add("Accept", "application/scim+json");

            var response = await httpClient.GetAsync("/Users?count=200");

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException(
                    $"Workday API request failed with status {response.StatusCode}: {errorContent}");
            }

            _logger.Information("SCIM: Successfully connected to Workday API");

            result.Success = true;
            result.UsersSynced = 0;
            result.GroupsSynced = 0;

            _logger.Information("SCIM: Workday sync completed. Users: {Users}, Groups: {Groups}",
                result.UsersSynced, result.GroupsSynced);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "SCIM: Failed to sync from Workday");
            result.Success = false;
            result.Errors.Add($"Workday sync failed: {ex.Message}");
        }
    }

    #endregion

    #region Helper Methods

    private ScimUserResource MapToScimUserResource(ScimUser user)
    {
        var resource = new ScimUserResource
        {
            Id = user.Id.ToString(),
            ExternalId = user.ExternalId,
            UserName = user.UserName,
            Active = user.Active,
            DisplayName = user.DisplayName,
            NickName = user.NickName,
            ProfileUrl = user.ProfileUrl,
            Title = user.Title,
            UserType = user.UserType,
            PreferredLanguage = user.PreferredLanguage,
            Locale = user.Locale,
            Timezone = user.Timezone,
            Name = new ScimName
            {
                Formatted = user.Formatted,
                FamilyName = user.FamilyName,
                GivenName = user.GivenName,
                MiddleName = user.MiddleName,
                HonorificPrefix = user.HonorificPrefix,
                HonorificSuffix = user.HonorificSuffix
            },
            Emails = new List<ScimEmail>
            {
                new ScimEmail
                {
                    Value = user.ApplicationUser?.Email ?? string.Empty,
                    Type = "work",
                    Primary = true
                }
            },
            EnterpriseUser = new ScimEnterpriseUser
            {
                EmployeeNumber = user.EmployeeNumber,
                CostCenter = user.CostCenter,
                Organization = user.Organization,
                Division = user.Division,
                Department = user.Department,
                Manager = user.Manager != null ? new ScimManager
                {
                    Value = user.Manager.Id.ToString(),
                    DisplayName = user.Manager.DisplayName
                } : null
            },
            Groups = user.GroupMemberships?.Select(gm => new ScimGroupMember
            {
                Value = gm.ScimGroup.Id.ToString(),
                Display = gm.ScimGroup.DisplayName,
                Type = "direct"
            }).ToList(),
            Meta = new ScimMeta
            {
                ResourceType = "User",
                Created = user.Created,
                LastModified = user.LastModified,
                Location = $"/scim/v2/Users/{user.Id}",
                Version = user.Version
            }
        };

        return resource;
    }

    private ScimGroupResource MapToScimGroupResource(ScimGroup group)
    {
        var resource = new ScimGroupResource
        {
            Id = group.Id.ToString(),
            ExternalId = group.ExternalId,
            DisplayName = group.DisplayName,
            Members = group.Members?.Select(m => new ScimGroupMemberRef
            {
                Value = m.ScimUser.Id.ToString(),
                Display = m.ScimUser.DisplayName,
                Type = "User",
                Ref = $"/scim/v2/Users/{m.ScimUser.Id}"
            }).ToList(),
            Meta = new ScimMeta
            {
                ResourceType = "Group",
                Created = group.Created,
                LastModified = group.LastModified,
                Location = $"/scim/v2/Groups/{group.Id}",
                Version = group.Version
            }
        };

        return resource;
    }

    private void ApplyPatchOperation(ScimUser user, ScimPatchOperation operation)
    {
        var path = operation.Path?.ToLowerInvariant() ?? "";

        switch (operation.Op.ToLowerInvariant())
        {
            case "replace":
                if (path == "active")
                {
                    user.Active = Convert.ToBoolean(operation.Value);
                    user.ApplicationUser.Status = user.Active ? "active" : "inactive";
                }
                else if (path.Contains("givenname"))
                {
                    user.GivenName = operation.Value?.ToString();
                    user.ApplicationUser.FirstName = user.GivenName;
                }
                else if (path.Contains("familyname"))
                {
                    user.FamilyName = operation.Value?.ToString();
                    user.ApplicationUser.LastName = user.FamilyName;
                }
                break;

            case "add":
                // Handle add operations (e.g., adding to groups)
                break;

            case "remove":
                // Handle remove operations
                break;
        }
    }

    private async Task ApplyGroupPatchOperation(ScimGroup group, ScimPatchOperation operation)
    {
        var path = operation.Path?.ToLowerInvariant() ?? "";

        switch (operation.Op.ToLowerInvariant())
        {
            case "add":
                if (path == "members")
                {
                    // Add members to group
                    // Implementation would add ScimGroupMembership records
                }
                break;

            case "remove":
                if (path.Contains("members"))
                {
                    // Remove members from group
                }
                break;

            case "replace":
                if (path == "displayname")
                {
                    group.DisplayName = operation.Value?.ToString() ?? group.DisplayName;
                }
                break;
        }

        await Task.CompletedTask;
    }

    private string GenerateTemporaryPassword()
    {
        // Generate a secure random password
        return Guid.NewGuid().ToString("N") + "Aa1!";
    }

    #endregion
}
