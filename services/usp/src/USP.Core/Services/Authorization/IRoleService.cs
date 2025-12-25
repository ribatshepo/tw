using USP.Core.Models.DTOs.Authorization;
using USP.Core.Models.Entities;

namespace USP.Core.Services.Authorization;

/// <summary>
/// Service for managing roles and permissions
/// </summary>
public interface IRoleService
{
    /// <summary>
    /// Get all roles
    /// </summary>
    Task<IEnumerable<RoleDto>> GetAllRolesAsync();

    /// <summary>
    /// Get role by ID
    /// </summary>
    Task<RoleDto?> GetRoleByIdAsync(Guid roleId);

    /// <summary>
    /// Get role by name
    /// </summary>
    Task<RoleDto?> GetRoleByNameAsync(string roleName);

    /// <summary>
    /// Create a new custom role
    /// </summary>
    Task<RoleDto> CreateRoleAsync(CreateRoleRequest request);

    /// <summary>
    /// Update role
    /// </summary>
    Task<bool> UpdateRoleAsync(Guid roleId, UpdateRoleRequest request);

    /// <summary>
    /// Delete role
    /// </summary>
    Task<bool> DeleteRoleAsync(Guid roleId);

    /// <summary>
    /// Assign permissions to a role
    /// </summary>
    Task<bool> AssignPermissionsAsync(Guid roleId, IEnumerable<string> permissionNames);

    /// <summary>
    /// Remove permissions from a role
    /// </summary>
    Task<bool> RemovePermissionsAsync(Guid roleId, IEnumerable<string> permissionNames);

    /// <summary>
    /// Get all permissions for a role
    /// </summary>
    Task<IEnumerable<PermissionDto>> GetRolePermissionsAsync(Guid roleId);

    /// <summary>
    /// Assign role to user
    /// </summary>
    Task<bool> AssignRoleToUserAsync(Guid userId, string roleName, Guid? namespaceId = null);

    /// <summary>
    /// Remove role from user
    /// </summary>
    Task<bool> RemoveRoleFromUserAsync(Guid userId, string roleName, Guid? namespaceId = null);

    /// <summary>
    /// Get all roles for a user
    /// </summary>
    Task<IEnumerable<string>> GetUserRolesAsync(Guid userId);

    /// <summary>
    /// Check if user has permission
    /// </summary>
    Task<bool> UserHasPermissionAsync(Guid userId, string permissionName);

    /// <summary>
    /// Initialize built-in roles and permissions
    /// </summary>
    Task InitializeBuiltInRolesAsync();
}
