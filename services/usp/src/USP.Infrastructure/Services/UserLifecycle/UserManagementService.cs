using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using USP.Core.Models.DTOs.UserLifecycle;
using USP.Core.Models.Entities;
using USP.Core.Services.Audit;
using USP.Core.Services.UserLifecycle;
using USP.Infrastructure.Data;

namespace USP.Infrastructure.Services.UserLifecycle;

/// <summary>
/// Service for managing user lifecycle operations
/// </summary>
public class UserManagementService : IUserManagementService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<Role> _roleManager;
    private readonly IAuditService _auditService;
    private readonly ILogger<UserManagementService> _logger;

    public UserManagementService(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<Role> roleManager,
        IAuditService auditService,
        ILogger<UserManagementService> logger)
    {
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
        _auditService = auditService;
        _logger = logger;
    }

    public async Task<UserDto> CreateUserAsync(CreateUserRequest request, Guid createdBy)
    {
        _logger.LogInformation("Creating user: {Username}, Email: {Email}", request.UserName, request.Email);

        // Check if username exists
        if (await _userManager.FindByNameAsync(request.UserName) != null)
        {
            throw new InvalidOperationException($"Username '{request.UserName}' is already taken");
        }

        // Check if email exists
        if (!string.IsNullOrEmpty(request.Email) && await _userManager.FindByEmailAsync(request.Email) != null)
        {
            throw new InvalidOperationException($"Email '{request.Email}' is already registered");
        }

        var user = new ApplicationUser
        {
            UserName = request.UserName,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            PhoneNumber = request.PhoneNumber,
            IsActive = true,
            Status = "active",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            EmailConfirmed = false
        };

        IdentityResult result;
        if (!string.IsNullOrEmpty(request.Password))
        {
            result = await _userManager.CreateAsync(user, request.Password);
        }
        else
        {
            // Generate random password if not provided
            var randomPassword = GenerateSecurePassword();
            result = await _userManager.CreateAsync(user, randomPassword);
        }

        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to create user: {errors}");
        }

        // Assign roles
        if (request.Roles.Any())
        {
            foreach (var roleName in request.Roles)
            {
                if (await _roleManager.RoleExistsAsync(roleName))
                {
                    await _userManager.AddToRoleAsync(user, roleName);
                }
                else
                {
                    _logger.LogWarning("Role {RoleName} does not exist", roleName);
                }
            }
        }

        await _auditService.LogAsync(
            action: "user.created",
            userId: createdBy,
            details: $"Created user: {user.UserName} ({user.Id})",
            resourceType: "User",
            resourceId: user.Id.ToString()
        );

        _logger.LogInformation("User created successfully: {UserId}, {Username}", user.Id, user.UserName);

        return await MapToUserDto(user);
    }

    public async Task<UserDto?> GetUserByIdAsync(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
            return null;

        return await MapToUserDto(user);
    }

    public async Task<UserDto?> GetUserByUsernameAsync(string username)
    {
        var user = await _userManager.FindByNameAsync(username);
        if (user == null)
            return null;

        return await MapToUserDto(user);
    }

    public async Task<UserDto?> GetUserByEmailAsync(string email)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null)
            return null;

        return await MapToUserDto(user);
    }

    public async Task<UserListResponse> ListUsersAsync(int page, int pageSize, string? search = null, string? status = null)
    {
        _logger.LogInformation("Listing users - Page: {Page}, PageSize: {PageSize}, Search: {Search}, Status: {Status}",
            page, pageSize, search, status);

        var query = _context.Users.AsQueryable();

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.ToLower();
            query = query.Where(u =>
                u.UserName!.ToLower().Contains(searchLower) ||
                (u.Email != null && u.Email.ToLower().Contains(searchLower)) ||
                (u.FirstName != null && u.FirstName.ToLower().Contains(searchLower)) ||
                (u.LastName != null && u.LastName.ToLower().Contains(searchLower))
            );
        }

        // Apply status filter
        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(u => u.Status == status);
        }

        var totalCount = await query.CountAsync();

        var users = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var userDtos = new List<UserDto>();
        foreach (var user in users)
        {
            userDtos.Add(await MapToUserDto(user));
        }

        return new UserListResponse
        {
            Users = userDtos,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        };
    }

    public async Task<UserDto> UpdateUserAsync(Guid userId, UpdateUserRequest request, Guid updatedBy)
    {
        _logger.LogInformation("Updating user: {UserId}", userId);

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            throw new InvalidOperationException("User not found");
        }

        // Check email uniqueness if changed
        if (!string.IsNullOrEmpty(request.Email) && request.Email != user.Email)
        {
            var existingUser = await _userManager.FindByEmailAsync(request.Email);
            if (existingUser != null && existingUser.Id != userId)
            {
                throw new InvalidOperationException($"Email '{request.Email}' is already registered");
            }
            user.Email = request.Email;
            user.EmailConfirmed = false; // Require re-verification
        }

        if (!string.IsNullOrEmpty(request.FirstName))
            user.FirstName = request.FirstName;

        if (!string.IsNullOrEmpty(request.LastName))
            user.LastName = request.LastName;

        if (!string.IsNullOrEmpty(request.PhoneNumber) && request.PhoneNumber != user.PhoneNumber)
        {
            user.PhoneNumber = request.PhoneNumber;
            user.PhoneNumberConfirmed = false; // Require re-verification
        }

        user.UpdatedAt = DateTime.UtcNow;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to update user: {errors}");
        }

        await _auditService.LogAsync(
            action: "user.updated",
            userId: updatedBy,
            details: $"Updated user: {user.UserName} ({user.Id})",
            resourceType: "User",
            resourceId: user.Id.ToString()
        );

        _logger.LogInformation("User updated successfully: {UserId}", userId);

        return await MapToUserDto(user);
    }

    public async Task DeleteUserAsync(Guid userId, Guid deletedBy)
    {
        _logger.LogInformation("Deleting user: {UserId}", userId);

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            throw new InvalidOperationException("User not found");
        }

        // Soft delete by setting IsActive to false and status to deleted
        user.IsActive = false;
        user.Status = "deleted";
        user.UpdatedAt = DateTime.UtcNow;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to delete user: {errors}");
        }

        await _auditService.LogAsync(
            action: "user.deleted",
            userId: deletedBy,
            details: $"Deleted user: {user.UserName} ({user.Id})",
            resourceType: "User",
            resourceId: user.Id.ToString()
        );

        _logger.LogInformation("User deleted successfully: {UserId}", userId);
    }

    public async Task DisableUserAsync(Guid userId, Guid disabledBy, string? reason = null)
    {
        _logger.LogInformation("Disabling user: {UserId}, Reason: {Reason}", userId, reason);

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            throw new InvalidOperationException("User not found");
        }

        user.IsActive = false;
        user.Status = "disabled";
        user.UpdatedAt = DateTime.UtcNow;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to disable user: {errors}");
        }

        await _auditService.LogAsync(
            action: "user.disabled",
            userId: disabledBy,
            details: $"Disabled user: {user.UserName} ({user.Id}). Reason: {reason}",
            resourceType: "User",
            resourceId: user.Id.ToString()
        );

        _logger.LogInformation("User disabled successfully: {UserId}", userId);
    }

    public async Task EnableUserAsync(Guid userId, Guid enabledBy)
    {
        _logger.LogInformation("Enabling user: {UserId}", userId);

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            throw new InvalidOperationException("User not found");
        }

        user.IsActive = true;
        user.Status = "active";
        user.UpdatedAt = DateTime.UtcNow;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to enable user: {errors}");
        }

        await _auditService.LogAsync(
            action: "user.enabled",
            userId: enabledBy,
            details: $"Enabled user: {user.UserName} ({user.Id})",
            resourceType: "User",
            resourceId: user.Id.ToString()
        );

        _logger.LogInformation("User enabled successfully: {UserId}", userId);
    }

    public async Task<UserDto> AssignRolesAsync(Guid userId, AssignRolesRequest request, Guid assignedBy)
    {
        _logger.LogInformation("Assigning roles to user: {UserId}, Roles: {Roles}",
            userId, string.Join(", ", request.Roles));

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            throw new InvalidOperationException("User not found");
        }

        // Remove existing roles
        var currentRoles = await _userManager.GetRolesAsync(user);
        if (currentRoles.Any())
        {
            var removeResult = await _userManager.RemoveFromRolesAsync(user, currentRoles);
            if (!removeResult.Succeeded)
            {
                var errors = string.Join(", ", removeResult.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Failed to remove current roles: {errors}");
            }
        }

        // Add new roles
        if (request.Roles.Any())
        {
            // Validate roles exist
            foreach (var roleName in request.Roles)
            {
                if (!await _roleManager.RoleExistsAsync(roleName))
                {
                    throw new InvalidOperationException($"Role '{roleName}' does not exist");
                }
            }

            var addResult = await _userManager.AddToRolesAsync(user, request.Roles);
            if (!addResult.Succeeded)
            {
                var errors = string.Join(", ", addResult.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Failed to assign roles: {errors}");
            }
        }

        await _auditService.LogAsync(
            action: "user.roles.assigned",
            userId: assignedBy,
            details: $"Assigned roles to user {user.UserName} ({user.Id}): {string.Join(", ", request.Roles)}",
            resourceType: "User",
            resourceId: user.Id.ToString()
        );

        _logger.LogInformation("Roles assigned successfully to user: {UserId}", userId);

        return await MapToUserDto(user);
    }

    public async Task<List<string>> GetUserRolesAsync(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            throw new InvalidOperationException("User not found");
        }

        var roles = await _userManager.GetRolesAsync(user);
        return roles.ToList();
    }

    public async Task<bool> UsernameExistsAsync(string username)
    {
        var user = await _userManager.FindByNameAsync(username);
        return user != null;
    }

    public async Task<bool> EmailExistsAsync(string email)
    {
        var user = await _userManager.FindByEmailAsync(email);
        return user != null;
    }

    private async Task<UserDto> MapToUserDto(ApplicationUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);

        return new UserDto
        {
            Id = user.Id,
            UserName = user.UserName ?? string.Empty,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            PhoneNumber = user.PhoneNumber,
            EmailConfirmed = user.EmailConfirmed,
            PhoneNumberConfirmed = user.PhoneNumberConfirmed,
            MfaEnabled = user.MfaEnabled,
            IsActive = user.IsActive,
            Status = user.Status,
            LastLoginAt = user.LastLoginAt,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt,
            Roles = roles.ToList()
        };
    }

    private static string GenerateSecurePassword()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*()_+-=[]{}|;:,.<>?";
        var random = new Random();
        return new string(Enumerable.Repeat(chars, 16)
            .Select(s => s[random.Next(s.Length)]).ToArray());
    }
}
