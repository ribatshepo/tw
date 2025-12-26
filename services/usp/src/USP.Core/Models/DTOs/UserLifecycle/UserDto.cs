namespace USP.Core.Models.DTOs.UserLifecycle;

/// <summary>
/// User data transfer object
/// </summary>
public class UserDto
{
    public Guid Id { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? PhoneNumber { get; set; }
    public bool EmailConfirmed { get; set; }
    public bool PhoneNumberConfirmed { get; set; }
    public bool MfaEnabled { get; set; }
    public bool IsActive { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<string> Roles { get; set; } = new();
}

/// <summary>
/// Request to create a new user
/// </summary>
public class CreateUserRequest
{
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Password { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? PhoneNumber { get; set; }
    public bool RequirePasswordChange { get; set; }
    public bool SendInvitationEmail { get; set; } = true;
    public List<string> Roles { get; set; } = new();
}

/// <summary>
/// Request to update user information
/// </summary>
public class UpdateUserRequest
{
    public string? Email { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? PhoneNumber { get; set; }
}

/// <summary>
/// Request to assign roles to a user
/// </summary>
public class AssignRolesRequest
{
    public List<string> Roles { get; set; } = new();
}

/// <summary>
/// Response for list users operation
/// </summary>
public class UserListResponse
{
    public List<UserDto> Users { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}
