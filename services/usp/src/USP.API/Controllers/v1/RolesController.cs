using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using USP.Core.Domain.Entities.Identity;
using USP.Core.Domain.Entities.Security;
using USP.Infrastructure.Persistence;

namespace USP.API.Controllers.v1;

/// <summary>
/// Role management endpoints for RBAC
/// </summary>
[ApiController]
[Route("api/v1/roles")]
[Authorize]
[Produces("application/json")]
public class RolesController : ControllerBase
{
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<RolesController> _logger;

    public RolesController(
        RoleManager<ApplicationRole> roleManager,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext context,
        ILogger<RolesController> logger)
    {
        _roleManager = roleManager;
        _userManager = userManager;
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Get all roles
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(RolesListResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRoles()
    {
        try
        {
            var roles = await _roleManager.Roles
                .Where(r => r.DeletedAt == null)
                .OrderBy(r => r.Name)
                .ToListAsync();

            var roleInfos = new List<RoleInfo>();
            foreach (var role in roles)
            {
                await _context.Entry(role)
                    .Collection(r => r.Permissions)
                    .LoadAsync();

                roleInfos.Add(new RoleInfo
                {
                    Id = role.Id,
                    Name = role.Name!,
                    Description = role.Description,
                    IsSystemRole = role.IsSystemRole,
                    Priority = role.Priority,
                    PermissionCount = role.Permissions.Count(p => p.DeletedAt == null),
                    CreatedAt = role.CreatedAt
                });
            }

            return Ok(new RolesListResponse
            {
                Roles = roleInfos
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting roles");
            return StatusCode(500, new { error = "Failed to get roles" });
        }
    }

    /// <summary>
    /// Get a specific role by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(RoleDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRole(string id)
    {
        try
        {
            var role = await _roleManager.FindByIdAsync(id);
            if (role == null || role.DeletedAt != null)
            {
                return NotFound(new { error = "Role not found" });
            }

            await _context.Entry(role)
                .Collection(r => r.Permissions)
                .LoadAsync();

            var permissions = role.Permissions
                .Where(p => p.DeletedAt == null)
                .Select(p => new PermissionInfo
                {
                    Id = p.Id,
                    Resource = p.Resource,
                    Action = p.Action,
                    Description = p.Description
                })
                .ToList();

            return Ok(new RoleDetailResponse
            {
                Id = role.Id,
                Name = role.Name!,
                Description = role.Description,
                IsSystemRole = role.IsSystemRole,
                Priority = role.Priority,
                Permissions = permissions,
                CreatedAt = role.CreatedAt,
                UpdatedAt = role.UpdatedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting role {RoleId}", id);
            return StatusCode(500, new { error = "Failed to get role" });
        }
    }

    /// <summary>
    /// Create a new role
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(RoleDetailResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateRole([FromBody] CreateRoleRequest request)
    {
        try
        {
            var role = new ApplicationRole
            {
                Name = request.Name,
                Description = request.Description,
                IsSystemRole = false,
                Priority = request.Priority ?? 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var result = await _roleManager.CreateAsync(role);
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                return BadRequest(new { error = $"Failed to create role: {errors}" });
            }

            _logger.LogInformation("Role created: {RoleId} - {RoleName}", role.Id, role.Name);

            return CreatedAtAction(
                nameof(GetRole),
                new { id = role.Id },
                new RoleDetailResponse
                {
                    Id = role.Id,
                    Name = role.Name!,
                    Description = role.Description,
                    IsSystemRole = role.IsSystemRole,
                    Priority = role.Priority,
                    Permissions = new List<PermissionInfo>(),
                    CreatedAt = role.CreatedAt,
                    UpdatedAt = role.UpdatedAt
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating role");
            return StatusCode(500, new { error = "Failed to create role" });
        }
    }

    /// <summary>
    /// Update an existing role
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(RoleDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateRole(string id, [FromBody] UpdateRoleRequest request)
    {
        try
        {
            var role = await _roleManager.FindByIdAsync(id);
            if (role == null || role.DeletedAt != null)
            {
                return NotFound(new { error = "Role not found" });
            }

            if (role.IsSystemRole)
            {
                return BadRequest(new { error = "Cannot modify system roles" });
            }

            if (!string.IsNullOrEmpty(request.Name))
            {
                role.Name = request.Name;
            }

            if (request.Description != null)
            {
                role.Description = request.Description;
            }

            if (request.Priority.HasValue)
            {
                role.Priority = request.Priority.Value;
            }

            role.UpdatedAt = DateTime.UtcNow;

            var result = await _roleManager.UpdateAsync(role);
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                return BadRequest(new { error = $"Failed to update role: {errors}" });
            }

            _logger.LogInformation("Role updated: {RoleId} - {RoleName}", role.Id, role.Name);

            await _context.Entry(role)
                .Collection(r => r.Permissions)
                .LoadAsync();

            var permissions = role.Permissions
                .Where(p => p.DeletedAt == null)
                .Select(p => new PermissionInfo
                {
                    Id = p.Id,
                    Resource = p.Resource,
                    Action = p.Action,
                    Description = p.Description
                })
                .ToList();

            return Ok(new RoleDetailResponse
            {
                Id = role.Id,
                Name = role.Name!,
                Description = role.Description,
                IsSystemRole = role.IsSystemRole,
                Priority = role.Priority,
                Permissions = permissions,
                CreatedAt = role.CreatedAt,
                UpdatedAt = role.UpdatedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating role {RoleId}", id);
            return StatusCode(500, new { error = "Failed to update role" });
        }
    }

    /// <summary>
    /// Delete a role (soft delete)
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteRole(string id)
    {
        try
        {
            var role = await _roleManager.FindByIdAsync(id);
            if (role == null || role.DeletedAt != null)
            {
                return NotFound(new { error = "Role not found" });
            }

            if (role.IsSystemRole)
            {
                return BadRequest(new { error = "Cannot delete system roles" });
            }

            // Soft delete
            role.DeletedAt = DateTime.UtcNow;
            await _roleManager.UpdateAsync(role);

            _logger.LogInformation("Role deleted: {RoleId} - {RoleName}", role.Id, role.Name);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting role {RoleId}", id);
            return StatusCode(500, new { error = "Failed to delete role" });
        }
    }

    /// <summary>
    /// Assign permissions to a role
    /// </summary>
    [HttpPost("{id}/permissions")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AssignPermissions(string id, [FromBody] AssignPermissionsRequest request)
    {
        try
        {
            var role = await _roleManager.FindByIdAsync(id);
            if (role == null || role.DeletedAt != null)
            {
                return NotFound(new { error = "Role not found" });
            }

            await _context.Entry(role)
                .Collection(r => r.Permissions)
                .LoadAsync();

            foreach (var permissionId in request.PermissionIds)
            {
                var permission = await _context.Set<Permission>()
                    .FirstOrDefaultAsync(p => p.Id == permissionId && p.DeletedAt == null);

                if (permission != null && !role.Permissions.Contains(permission))
                {
                    role.Permissions.Add(permission);
                }
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Assigned {Count} permissions to role {RoleId}",
                request.PermissionIds.Count, id);

            return Ok(new { message = "Permissions assigned successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning permissions to role {RoleId}", id);
            return StatusCode(500, new { error = "Failed to assign permissions" });
        }
    }

    /// <summary>
    /// Remove permissions from a role
    /// </summary>
    [HttpDelete("{id}/permissions")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemovePermissions(string id, [FromBody] RemovePermissionsRequest request)
    {
        try
        {
            var role = await _roleManager.FindByIdAsync(id);
            if (role == null || role.DeletedAt != null)
            {
                return NotFound(new { error = "Role not found" });
            }

            await _context.Entry(role)
                .Collection(r => r.Permissions)
                .LoadAsync();

            foreach (var permissionId in request.PermissionIds)
            {
                var permission = role.Permissions.FirstOrDefault(p => p.Id == permissionId);
                if (permission != null)
                {
                    role.Permissions.Remove(permission);
                }
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Removed {Count} permissions from role {RoleId}",
                request.PermissionIds.Count, id);

            return Ok(new { message = "Permissions removed successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing permissions from role {RoleId}", id);
            return StatusCode(500, new { error = "Failed to remove permissions" });
        }
    }

    /// <summary>
    /// Get all users assigned to a role
    /// </summary>
    [HttpGet("{id}/users")]
    [ProducesResponseType(typeof(RoleUsersResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRoleUsers(string id)
    {
        try
        {
            var role = await _roleManager.FindByIdAsync(id);
            if (role == null || role.DeletedAt == null)
            {
                return NotFound(new { error = "Role not found" });
            }

            var users = await _userManager.GetUsersInRoleAsync(role.Name!);

            var userInfos = users.Select(u => new UserInfo
            {
                Id = u.Id,
                Username = u.UserName!,
                Email = u.Email!
            }).ToList();

            return Ok(new RoleUsersResponse
            {
                RoleId = id,
                RoleName = role.Name!,
                Users = userInfos
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting users for role {RoleId}", id);
            return StatusCode(500, new { error = "Failed to get role users" });
        }
    }
}

// DTOs

public class RolesListResponse
{
    public required List<RoleInfo> Roles { get; set; }
}

public class RoleInfo
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public bool IsSystemRole { get; set; }
    public int Priority { get; set; }
    public int PermissionCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class RoleDetailResponse
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public bool IsSystemRole { get; set; }
    public int Priority { get; set; }
    public required List<PermissionInfo> Permissions { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class PermissionInfo
{
    public required string Id { get; set; }
    public required string Resource { get; set; }
    public required string Action { get; set; }
    public string? Description { get; set; }
}

public class CreateRoleRequest
{
    public required string Name { get; set; }
    public string? Description { get; set; }
    public int? Priority { get; set; }
}

public class UpdateRoleRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public int? Priority { get; set; }
}

public class AssignPermissionsRequest
{
    public required List<string> PermissionIds { get; set; }
}

public class RemovePermissionsRequest
{
    public required List<string> PermissionIds { get; set; }
}

public class RoleUsersResponse
{
    public required string RoleId { get; set; }
    public required string RoleName { get; set; }
    public required List<UserInfo> Users { get; set; }
}

public class UserInfo
{
    public required string Id { get; set; }
    public required string Username { get; set; }
    public required string Email { get; set; }
}
