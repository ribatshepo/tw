using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using USP.Core.Models.DTOs.UserLifecycle;
using USP.Core.Services.UserLifecycle;

namespace USP.Api.Controllers.UserLifecycle;

/// <summary>
/// Controller for user lifecycle management
/// </summary>
[ApiController]
[Route("api/v1/users")]
[Authorize]
[Produces("application/json")]
public class UserController : ControllerBase
{
    private readonly IUserManagementService _userManagementService;
    private readonly ILogger<UserController> _logger;

    public UserController(
        IUserManagementService userManagementService,
        ILogger<UserController> logger)
    {
        _userManagementService = userManagementService;
        _logger = logger;
    }

    /// <summary>
    /// Create a new user
    /// </summary>
    /// <param name="request">User creation request</param>
    /// <returns>Created user information</returns>
    [HttpPost]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<UserDto>> CreateUser([FromBody] CreateUserRequest request)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            var user = await _userManagementService.CreateUserAsync(request, currentUserId);

            _logger.LogInformation("User created: {UserId}, {Username}", user.Id, user.UserName);

            return CreatedAtAction(nameof(GetUser), new { id = user.Id }, user);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Failed to create user: {Message}", ex.Message);
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "User creation failed",
                Detail = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user: {Username}", request.UserName);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal server error",
                Detail = "An unexpected error occurred while creating the user"
            });
        }
    }

    /// <summary>
    /// Get user by ID
    /// </summary>
    /// <param name="id">User ID</param>
    /// <returns>User information</returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<UserDto>> GetUser(Guid id)
    {
        try
        {
            var user = await _userManagementService.GetUserByIdAsync(id);

            if (user == null)
            {
                return NotFound(new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "User not found",
                    Detail = $"User with ID '{id}' was not found"
                });
            }

            return Ok(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user: {UserId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal server error",
                Detail = "An unexpected error occurred while retrieving the user"
            });
        }
    }

    /// <summary>
    /// List users with pagination and filtering
    /// </summary>
    /// <param name="page">Page number (default: 1)</param>
    /// <param name="pageSize">Page size (default: 20)</param>
    /// <param name="search">Search term for username, email, or name</param>
    /// <param name="status">Filter by user status</param>
    /// <returns>Paginated list of users</returns>
    [HttpGet]
    [ProducesResponseType(typeof(UserListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<UserListResponse>> ListUsers(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null)
    {
        try
        {
            if (page < 1 || pageSize < 1 || pageSize > 100)
            {
                return BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Invalid parameters",
                    Detail = "Page must be >= 1 and pageSize must be between 1 and 100"
                });
            }

            var response = await _userManagementService.ListUsersAsync(page, pageSize, search, status);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing users");
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal server error",
                Detail = "An unexpected error occurred while listing users"
            });
        }
    }

    /// <summary>
    /// Update user information
    /// </summary>
    /// <param name="id">User ID</param>
    /// <param name="request">Update request</param>
    /// <returns>Updated user information</returns>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<UserDto>> UpdateUser(Guid id, [FromBody] UpdateUserRequest request)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            var user = await _userManagementService.UpdateUserAsync(id, request, currentUserId);

            _logger.LogInformation("User updated: {UserId}", id);

            return Ok(user);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Failed to update user {UserId}: {Message}", id, ex.Message);

            if (ex.Message.Contains("not found"))
            {
                return NotFound(new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "User not found",
                    Detail = ex.Message
                });
            }

            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "User update failed",
                Detail = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user: {UserId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal server error",
                Detail = "An unexpected error occurred while updating the user"
            });
        }
    }

    /// <summary>
    /// Delete user (soft delete)
    /// </summary>
    /// <param name="id">User ID</param>
    /// <returns>No content on success</returns>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteUser(Guid id)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            await _userManagementService.DeleteUserAsync(id, currentUserId);

            _logger.LogInformation("User deleted: {UserId}", id);

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Failed to delete user {UserId}: {Message}", id, ex.Message);

            if (ex.Message.Contains("not found"))
            {
                return NotFound(new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "User not found",
                    Detail = ex.Message
                });
            }

            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "User deletion failed",
                Detail = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting user: {UserId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal server error",
                Detail = "An unexpected error occurred while deleting the user"
            });
        }
    }

    /// <summary>
    /// Disable user account
    /// </summary>
    /// <param name="id">User ID</param>
    /// <param name="reason">Reason for disabling (optional)</param>
    /// <returns>No content on success</returns>
    [HttpPost("{id:guid}/disable")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DisableUser(Guid id, [FromQuery] string? reason = null)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            await _userManagementService.DisableUserAsync(id, currentUserId, reason);

            _logger.LogInformation("User disabled: {UserId}, Reason: {Reason}", id, reason);

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Failed to disable user {UserId}: {Message}", id, ex.Message);

            if (ex.Message.Contains("not found"))
            {
                return NotFound(new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "User not found",
                    Detail = ex.Message
                });
            }

            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "User disable failed",
                Detail = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disabling user: {UserId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal server error",
                Detail = "An unexpected error occurred while disabling the user"
            });
        }
    }

    /// <summary>
    /// Enable user account
    /// </summary>
    /// <param name="id">User ID</param>
    /// <returns>No content on success</returns>
    [HttpPost("{id:guid}/enable")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> EnableUser(Guid id)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            await _userManagementService.EnableUserAsync(id, currentUserId);

            _logger.LogInformation("User enabled: {UserId}", id);

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Failed to enable user {UserId}: {Message}", id, ex.Message);

            if (ex.Message.Contains("not found"))
            {
                return NotFound(new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "User not found",
                    Detail = ex.Message
                });
            }

            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "User enable failed",
                Detail = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enabling user: {UserId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal server error",
                Detail = "An unexpected error occurred while enabling the user"
            });
        }
    }

    /// <summary>
    /// Assign roles to user
    /// </summary>
    /// <param name="id">User ID</param>
    /// <param name="request">Roles to assign</param>
    /// <returns>Updated user information</returns>
    [HttpPost("{id:guid}/roles")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<UserDto>> AssignRoles(Guid id, [FromBody] AssignRolesRequest request)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            var user = await _userManagementService.AssignRolesAsync(id, request, currentUserId);

            _logger.LogInformation("Roles assigned to user: {UserId}, Roles: {Roles}",
                id, string.Join(", ", request.Roles));

            return Ok(user);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Failed to assign roles to user {UserId}: {Message}", id, ex.Message);

            if (ex.Message.Contains("not found"))
            {
                return NotFound(new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "User or role not found",
                    Detail = ex.Message
                });
            }

            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Role assignment failed",
                Detail = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning roles to user: {UserId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal server error",
                Detail = "An unexpected error occurred while assigning roles"
            });
        }
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            throw new UnauthorizedAccessException("User ID not found in token");
        }
        return userId;
    }
}
