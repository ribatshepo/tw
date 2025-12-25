using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using USP.Core.Models.DTOs.Authorization;
using USP.Core.Models.Entities;
using USP.Core.Services.Authorization;
using USP.Infrastructure.Data;

namespace USP.Infrastructure.Services.Authorization;

/// <summary>
/// Service for managing roles and permissions
/// </summary>
public class RoleService : IRoleService
{
    private readonly ApplicationDbContext _context;
    private readonly RoleManager<Role> _roleManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<RoleService> _logger;

    public RoleService(
        ApplicationDbContext context,
        RoleManager<Role> roleManager,
        UserManager<ApplicationUser> userManager,
        ILogger<RoleService> logger)
    {
        _context = context;
        _roleManager = roleManager;
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<IEnumerable<RoleDto>> GetAllRolesAsync()
    {
        var roles = await _context.Roles
            .Include(r => r.RolePermissions)
            .ThenInclude(rp => rp.Permission)
            .AsNoTracking()
            .ToListAsync();

        return roles.Select(MapToDto);
    }

    public async Task<RoleDto?> GetRoleByIdAsync(Guid roleId)
    {
        var role = await _context.Roles
            .Include(r => r.RolePermissions)
            .ThenInclude(rp => rp.Permission)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == roleId);

        return role != null ? MapToDto(role) : null;
    }

    public async Task<RoleDto?> GetRoleByNameAsync(string roleName)
    {
        var role = await _context.Roles
            .Include(r => r.RolePermissions)
            .ThenInclude(rp => rp.Permission)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Name == roleName);

        return role != null ? MapToDto(role) : null;
    }

    public async Task<RoleDto> CreateRoleAsync(CreateRoleRequest request)
    {
        // Check if role already exists
        if (await _roleManager.RoleExistsAsync(request.Name))
        {
            throw new InvalidOperationException($"Role '{request.Name}' already exists");
        }

        var role = new Role
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            IsBuiltIn = false,
            CreatedAt = DateTime.UtcNow
        };

        var result = await _roleManager.CreateAsync(role);

        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to create role: {errors}");
        }

        // Assign permissions if provided
        if (request.Permissions.Any())
        {
            await AssignPermissionsAsync(role.Id, request.Permissions);
        }

        _logger.LogInformation("Role '{RoleName}' created successfully", request.Name);

        // Reload role with permissions
        var createdRole = await _context.Roles
            .Include(r => r.RolePermissions)
            .ThenInclude(rp => rp.Permission)
            .FirstAsync(r => r.Id == role.Id);

        return MapToDto(createdRole);
    }

    public async Task<bool> UpdateRoleAsync(Guid roleId, UpdateRoleRequest request)
    {
        var role = await _context.Roles.FindAsync(roleId);

        if (role == null)
        {
            return false;
        }

        if (role.IsBuiltIn)
        {
            throw new InvalidOperationException("Cannot modify built-in roles");
        }

        role.Description = request.Description;
        role.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Role '{RoleName}' updated successfully", role.Name);

        return true;
    }

    public async Task<bool> DeleteRoleAsync(Guid roleId)
    {
        var role = await _context.Roles.FindAsync(roleId);

        if (role == null)
        {
            return false;
        }

        if (role.IsBuiltIn)
        {
            throw new InvalidOperationException("Cannot delete built-in roles");
        }

        // Check if role is assigned to any users
        var hasUsers = await _context.UserRoles.AnyAsync(ur => ur.RoleId == roleId);
        if (hasUsers)
        {
            throw new InvalidOperationException("Cannot delete role that is assigned to users");
        }

        var result = await _roleManager.DeleteAsync(role);

        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to delete role: {errors}");
        }

        _logger.LogInformation("Role '{RoleName}' deleted successfully", role.Name);

        return true;
    }

    public async Task<bool> AssignPermissionsAsync(Guid roleId, IEnumerable<string> permissionNames)
    {
        var role = await _context.Roles.FindAsync(roleId);

        if (role == null)
        {
            return false;
        }

        foreach (var permissionName in permissionNames)
        {
            var permission = await _context.Permissions
                .FirstOrDefaultAsync(p => p.Name == permissionName);

            if (permission == null)
            {
                _logger.LogWarning("Permission '{PermissionName}' not found", permissionName);
                continue;
            }

            // Check if already assigned
            var exists = await _context.RolePermissions
                .AnyAsync(rp => rp.RoleId == roleId && rp.PermissionId == permission.Id);

            if (!exists)
            {
                _context.RolePermissions.Add(new RolePermission
                {
                    RoleId = roleId,
                    PermissionId = permission.Id,
                    AssignedAt = DateTime.UtcNow
                });
            }
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Permissions assigned to role '{RoleName}'", role.Name);

        return true;
    }

    public async Task<bool> RemovePermissionsAsync(Guid roleId, IEnumerable<string> permissionNames)
    {
        var role = await _context.Roles.FindAsync(roleId);

        if (role == null)
        {
            return false;
        }

        if (role.IsBuiltIn)
        {
            throw new InvalidOperationException("Cannot modify permissions of built-in roles");
        }

        var permissions = await _context.Permissions
            .Where(p => permissionNames.Contains(p.Name))
            .ToListAsync();

        var permissionIds = permissions.Select(p => p.Id).ToList();

        var rolePermissions = await _context.RolePermissions
            .Where(rp => rp.RoleId == roleId && permissionIds.Contains(rp.PermissionId))
            .ToListAsync();

        _context.RolePermissions.RemoveRange(rolePermissions);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Permissions removed from role '{RoleName}'", role.Name);

        return true;
    }

    public async Task<IEnumerable<PermissionDto>> GetRolePermissionsAsync(Guid roleId)
    {
        var permissions = await _context.RolePermissions
            .Where(rp => rp.RoleId == roleId)
            .Include(rp => rp.Permission)
            .Select(rp => rp.Permission)
            .AsNoTracking()
            .ToListAsync();

        return permissions.Select(MapPermissionToDto);
    }

    public async Task<bool> AssignRoleToUserAsync(Guid userId, string roleName, Guid? namespaceId = null)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());

        if (user == null)
        {
            return false;
        }

        var role = await _roleManager.FindByNameAsync(roleName);

        if (role == null)
        {
            throw new InvalidOperationException($"Role '{roleName}' not found");
        }

        // Check if user already has this role in this namespace
        var exists = await _context.UserRoles
            .AnyAsync(ur => ur.UserId == userId && ur.RoleId == role.Id && ur.NamespaceId == namespaceId);

        if (exists)
        {
            return true; // Already assigned
        }

        var userRole = new UserRole
        {
            UserId = userId,
            RoleId = role.Id,
            NamespaceId = namespaceId,
            AssignedAt = DateTime.UtcNow
        };

        _context.UserRoles.Add(userRole);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Role '{RoleName}' assigned to user {UserId}", roleName, userId);

        return true;
    }

    public async Task<bool> RemoveRoleFromUserAsync(Guid userId, string roleName, Guid? namespaceId = null)
    {
        var role = await _roleManager.FindByNameAsync(roleName);

        if (role == null)
        {
            return false;
        }

        var userRole = await _context.UserRoles
            .FirstOrDefaultAsync(ur => ur.UserId == userId && ur.RoleId == role.Id && ur.NamespaceId == namespaceId);

        if (userRole == null)
        {
            return false;
        }

        _context.UserRoles.Remove(userRole);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Role '{RoleName}' removed from user {UserId}", roleName, userId);

        return true;
    }

    public async Task<IEnumerable<string>> GetUserRolesAsync(Guid userId)
    {
        var roleIds = await _context.UserRoles
            .Where(ur => ur.UserId == userId)
            .Select(ur => ur.RoleId)
            .ToListAsync();

        var roles = await _context.Roles
            .Where(r => roleIds.Contains(r.Id))
            .Select(r => r.Name)
            .ToListAsync();

        return roles!;
    }

    public async Task<bool> UserHasPermissionAsync(Guid userId, string permissionName)
    {
        // Get user's roles
        var roleIds = await _context.UserRoles
            .Where(ur => ur.UserId == userId)
            .Select(ur => ur.RoleId)
            .ToListAsync();

        // Check if any of the user's roles have the permission
        var hasPermission = await _context.RolePermissions
            .Include(rp => rp.Permission)
            .AnyAsync(rp => roleIds.Contains(rp.RoleId) && rp.Permission.Name == permissionName);

        return hasPermission;
    }

    public async Task InitializeBuiltInRolesAsync()
    {
        _logger.LogInformation("Initializing built-in roles and permissions...");

        // Initialize permissions first
        await InitializePermissionsAsync();

        // Define built-in roles
        var builtInRoles = new List<(string Name, string Description, string[] Permissions)>
        {
            ("PlatformAdmin", "Full platform control", new[]
            {
                "platform:manage", "platform:view", "users:create", "users:read", "users:update", "users:delete",
                "roles:manage", "permissions:assign", "secrets:read", "secrets:write", "secrets:delete", "secrets:rotate",
                "policies:create", "policies:update", "policies:delete", "catalog:view", "catalog:edit", "catalog:delete",
                "workspace:create", "workspace:manage", "workspace:delete", "pam:checkout", "pam:manage-safes", "pam:view-sessions",
                "audit:read", "audit:export", "compliance:view-reports", "compliance:generate-reports",
                "data:import", "data:export", "data:transform", "api-keys:create", "api-keys:revoke",
                "certificates:issue", "certificates:revoke"
            }),
            ("SystemAdmin", "System configuration", new[]
            {
                "platform:view", "users:read", "users:update", "secrets:read", "secrets:write",
                "catalog:view", "catalog:edit", "workspace:manage", "audit:read"
            }),
            ("SecurityAdmin", "Security policy management", new[]
            {
                "users:read", "roles:manage", "permissions:assign", "policies:create", "policies:update", "policies:delete",
                "audit:read", "audit:export", "compliance:view-reports"
            }),
            ("AuthAdmin", "User and authentication management", new[]
            {
                "users:create", "users:read", "users:update", "users:delete", "roles:manage", "audit:read"
            }),
            ("SecretsAdmin", "Secrets management administration", new[]
            {
                "secrets:read", "secrets:write", "secrets:delete", "secrets:rotate", "audit:read"
            }),
            ("CatalogAdmin", "Data catalog administration", new[]
            {
                "catalog:view", "catalog:edit", "catalog:delete", "audit:read"
            }),
            ("PAMAdmin", "Privileged access management", new[]
            {
                "pam:checkout", "pam:manage-safes", "pam:view-sessions", "audit:read", "audit:export"
            }),
            ("ComplianceOfficer", "Compliance and audit access", new[]
            {
                "audit:read", "audit:export", "compliance:view-reports", "compliance:generate-reports"
            }),
            ("WorkspaceOwner", "Workspace administration", new[]
            {
                "workspace:create", "workspace:manage", "workspace:delete", "users:read", "catalog:view", "catalog:edit"
            }),
            ("WorkspaceAdmin", "Workspace management", new[]
            {
                "workspace:manage", "users:read", "catalog:view", "catalog:edit"
            }),
            ("DataEngineer", "Data operations", new[]
            {
                "data:import", "data:export", "data:transform", "catalog:view", "catalog:edit", "secrets:read"
            }),
            ("DataAnalyst", "Read-only analytics", new[]
            {
                "catalog:view", "data:export"
            }),
            ("Developer", "Development access", new[]
            {
                "secrets:read", "api-keys:create", "api-keys:revoke", "catalog:view"
            }),
            ("User", "Standard user access", new[]
            {
                "catalog:view", "secrets:read"
            }),
            ("Viewer", "Read-only access", new[]
            {
                "platform:view", "catalog:view"
            }),
            ("Auditor", "Audit log access only", new[]
            {
                "audit:read"
            })
        };

        foreach (var (name, description, permissions) in builtInRoles)
        {
            var roleExists = await _roleManager.RoleExistsAsync(name);

            if (!roleExists)
            {
                var role = new Role
                {
                    Id = Guid.NewGuid(),
                    Name = name,
                    Description = description,
                    IsBuiltIn = true,
                    CreatedAt = DateTime.UtcNow
                };

                var result = await _roleManager.CreateAsync(role);

                if (result.Succeeded)
                {
                    // Assign permissions
                    await AssignPermissionsAsync(role.Id, permissions);
                    _logger.LogInformation("Created built-in role: {RoleName}", name);
                }
                else
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    _logger.LogError("Failed to create role {RoleName}: {Errors}", name, errors);
                }
            }
            else
            {
                _logger.LogDebug("Built-in role already exists: {RoleName}", name);
            }
        }

        _logger.LogInformation("Built-in roles initialization completed");
    }

    private async Task InitializePermissionsAsync()
    {
        var permissions = new List<(string Name, string Description, string Resource, string Action)>
        {
            // Platform permissions
            ("platform:manage", "Manage platform settings", "platform", "manage"),
            ("platform:view", "View platform information", "platform", "view"),

            // User permissions
            ("users:create", "Create users", "users", "create"),
            ("users:read", "Read user information", "users", "read"),
            ("users:update", "Update user information", "users", "update"),
            ("users:delete", "Delete users", "users", "delete"),

            // Role permissions
            ("roles:manage", "Manage roles", "roles", "manage"),
            ("permissions:assign", "Assign permissions", "permissions", "assign"),

            // Secret permissions
            ("secrets:read", "Read secrets", "secrets", "read"),
            ("secrets:write", "Write secrets", "secrets", "write"),
            ("secrets:delete", "Delete secrets", "secrets", "delete"),
            ("secrets:rotate", "Rotate secrets", "secrets", "rotate"),

            // Policy permissions
            ("policies:create", "Create policies", "policies", "create"),
            ("policies:update", "Update policies", "policies", "update"),
            ("policies:delete", "Delete policies", "policies", "delete"),

            // Catalog permissions
            ("catalog:view", "View catalog", "catalog", "view"),
            ("catalog:edit", "Edit catalog", "catalog", "edit"),
            ("catalog:delete", "Delete catalog entries", "catalog", "delete"),

            // Workspace permissions
            ("workspace:create", "Create workspaces", "workspace", "create"),
            ("workspace:manage", "Manage workspaces", "workspace", "manage"),
            ("workspace:delete", "Delete workspaces", "workspace", "delete"),

            // PAM permissions
            ("pam:checkout", "Checkout privileged accounts", "pam", "checkout"),
            ("pam:manage-safes", "Manage PAM safes", "pam", "manage-safes"),
            ("pam:view-sessions", "View PAM sessions", "pam", "view-sessions"),

            // Audit permissions
            ("audit:read", "Read audit logs", "audit", "read"),
            ("audit:export", "Export audit logs", "audit", "export"),

            // Compliance permissions
            ("compliance:view-reports", "View compliance reports", "compliance", "view-reports"),
            ("compliance:generate-reports", "Generate compliance reports", "compliance", "generate-reports"),

            // Data permissions
            ("data:import", "Import data", "data", "import"),
            ("data:export", "Export data", "data", "export"),
            ("data:transform", "Transform data", "data", "transform"),

            // API key permissions
            ("api-keys:create", "Create API keys", "api-keys", "create"),
            ("api-keys:revoke", "Revoke API keys", "api-keys", "revoke"),

            // Certificate permissions
            ("certificates:issue", "Issue certificates", "certificates", "issue"),
            ("certificates:revoke", "Revoke certificates", "certificates", "revoke")
        };

        foreach (var (name, description, resource, action) in permissions)
        {
            var exists = await _context.Permissions.AnyAsync(p => p.Name == name);

            if (!exists)
            {
                _context.Permissions.Add(new Permission
                {
                    Id = Guid.NewGuid(),
                    Name = name,
                    Description = description,
                    Resource = resource,
                    Action = action,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("Permissions initialization completed");
    }

    private static RoleDto MapToDto(Role role) => new()
    {
        Id = role.Id,
        Name = role.Name ?? string.Empty,
        Description = role.Description,
        IsBuiltIn = role.IsBuiltIn,
        CreatedAt = role.CreatedAt,
        UpdatedAt = role.UpdatedAt,
        Permissions = role.RolePermissions?.Select(rp => MapPermissionToDto(rp.Permission)).ToList() ?? new()
    };

    private static PermissionDto MapPermissionToDto(Permission permission) => new()
    {
        Id = permission.Id,
        Name = permission.Name,
        Description = permission.Description,
        Resource = permission.Resource,
        Action = permission.Action
    };
}
