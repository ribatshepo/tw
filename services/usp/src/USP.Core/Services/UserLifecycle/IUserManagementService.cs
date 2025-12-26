using USP.Core.Models.DTOs.UserLifecycle;

namespace USP.Core.Services.UserLifecycle;

/// <summary>
/// Service for managing user lifecycle operations
/// </summary>
public interface IUserManagementService
{
    /// <summary>
    /// Create a new user
    /// </summary>
    Task<UserDto> CreateUserAsync(CreateUserRequest request, Guid createdBy);

    /// <summary>
    /// Get user by ID
    /// </summary>
    Task<UserDto?> GetUserByIdAsync(Guid userId);

    /// <summary>
    /// Get user by username
    /// </summary>
    Task<UserDto?> GetUserByUsernameAsync(string username);

    /// <summary>
    /// Get user by email
    /// </summary>
    Task<UserDto?> GetUserByEmailAsync(string email);

    /// <summary>
    /// List users with pagination
    /// </summary>
    Task<UserListResponse> ListUsersAsync(int page, int pageSize, string? search = null, string? status = null);

    /// <summary>
    /// Update user information
    /// </summary>
    Task<UserDto> UpdateUserAsync(Guid userId, UpdateUserRequest request, Guid updatedBy);

    /// <summary>
    /// Delete user (soft delete)
    /// </summary>
    Task DeleteUserAsync(Guid userId, Guid deletedBy);

    /// <summary>
    /// Disable user account
    /// </summary>
    Task DisableUserAsync(Guid userId, Guid disabledBy, string? reason = null);

    /// <summary>
    /// Enable user account
    /// </summary>
    Task EnableUserAsync(Guid userId, Guid enabledBy);

    /// <summary>
    /// Assign roles to user
    /// </summary>
    Task<UserDto> AssignRolesAsync(Guid userId, AssignRolesRequest request, Guid assignedBy);

    /// <summary>
    /// Get user roles
    /// </summary>
    Task<List<string>> GetUserRolesAsync(Guid userId);

    /// <summary>
    /// Check if username exists
    /// </summary>
    Task<bool> UsernameExistsAsync(string username);

    /// <summary>
    /// Check if email exists
    /// </summary>
    Task<bool> EmailExistsAsync(string email);
}
