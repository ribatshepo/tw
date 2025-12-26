using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using USP.Core.Models.DTOs.SCIM;
using USP.Infrastructure.Services.UserLifecycle;

namespace USP.Api.Controllers.SCIM;

/// <summary>
/// SCIM 2.0 (System for Cross-domain Identity Management) Controller
/// </summary>
[ApiController]
[Route("api/v1/scim")]
[Authorize]
[Produces("application/scim+json", "application/json")]
public class ScimController : ControllerBase
{
    private readonly IScimProviderService _scimService;
    private readonly ILogger<ScimController> _logger;

    public ScimController(
        IScimProviderService scimService,
        ILogger<ScimController> logger)
    {
        _scimService = scimService;
        _logger = logger;
    }

    #region User Operations

    /// <summary>
    /// List users (SCIM 2.0)
    /// </summary>
    /// <param name="filter">SCIM filter expression</param>
    /// <param name="startIndex">Starting index (1-based)</param>
    /// <param name="count">Number of results to return</param>
    /// <param name="attributes">Comma-separated list of attributes to return</param>
    /// <returns>SCIM list response with users</returns>
    [HttpGet("Users")]
    [ProducesResponseType(typeof(ScimListResponse<ScimUserResource>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ScimError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ScimError), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ScimListResponse<ScimUserResource>>> GetUsers(
        [FromQuery] string? filter = null,
        [FromQuery] int startIndex = 1,
        [FromQuery] int count = 20,
        [FromQuery] string? attributes = null)
    {
        try
        {
            if (startIndex < 1)
            {
                return BadRequest(CreateScimError(
                    "invalidValue",
                    "startIndex must be greater than or equal to 1",
                    StatusCodes.Status400BadRequest
                ));
            }

            if (count < 0 || count > 100)
            {
                return BadRequest(CreateScimError(
                    "invalidValue",
                    "count must be between 0 and 100",
                    StatusCodes.Status400BadRequest
                ));
            }

            var response = await _scimService.GetUsersAsync(filter, startIndex, count, attributes);

            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Invalid SCIM filter: {Filter}, Error: {Message}", filter, ex.Message);
            return BadRequest(CreateScimError(
                "invalidFilter",
                ex.Message,
                StatusCodes.Status400BadRequest
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing SCIM users");
            return StatusCode(StatusCodes.Status500InternalServerError, CreateScimError(
                "internalServerError",
                "An unexpected error occurred while listing users",
                StatusCodes.Status500InternalServerError
            ));
        }
    }

    /// <summary>
    /// Get user by ID (SCIM 2.0)
    /// </summary>
    /// <param name="id">User ID</param>
    /// <param name="attributes">Comma-separated list of attributes to return</param>
    /// <returns>SCIM user resource</returns>
    [HttpGet("Users/{id:guid}")]
    [ProducesResponseType(typeof(ScimUserResource), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ScimError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ScimError), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ScimUserResource>> GetUser(
        Guid id,
        [FromQuery] string? attributes = null)
    {
        try
        {
            var user = await _scimService.GetUserByIdAsync(id, attributes);

            if (user == null)
            {
                return NotFound(CreateScimError(
                    "resourceNotFound",
                    $"User with ID '{id}' was not found",
                    StatusCodes.Status404NotFound
                ));
            }

            return Ok(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving SCIM user: {UserId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, CreateScimError(
                "internalServerError",
                "An unexpected error occurred while retrieving the user",
                StatusCodes.Status500InternalServerError
            ));
        }
    }

    /// <summary>
    /// Create user (SCIM 2.0)
    /// </summary>
    /// <param name="user">SCIM user resource</param>
    /// <returns>Created SCIM user resource</returns>
    [HttpPost("Users")]
    [ProducesResponseType(typeof(ScimUserResource), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ScimError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ScimError), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ScimError), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ScimUserResource>> CreateUser([FromBody] ScimUserResource user)
    {
        try
        {
            var createdUser = await _scimService.CreateUserAsync(user);

            _logger.LogInformation("SCIM user created: {UserId}, {Username}", createdUser.Id, createdUser.UserName);

            return CreatedAtAction(
                nameof(GetUser),
                new { id = createdUser.Id },
                createdUser
            );
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Failed to create SCIM user: {Message}", ex.Message);

            if (ex.Message.Contains("already") || ex.Message.Contains("exists"))
            {
                return Conflict(CreateScimError(
                    "uniqueness",
                    ex.Message,
                    StatusCodes.Status409Conflict
                ));
            }

            return BadRequest(CreateScimError(
                "invalidValue",
                ex.Message,
                StatusCodes.Status400BadRequest
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating SCIM user");
            return StatusCode(StatusCodes.Status500InternalServerError, CreateScimError(
                "internalServerError",
                "An unexpected error occurred while creating the user",
                StatusCodes.Status500InternalServerError
            ));
        }
    }

    /// <summary>
    /// Update user (SCIM 2.0) - Full update
    /// </summary>
    /// <param name="id">User ID</param>
    /// <param name="user">SCIM user resource</param>
    /// <returns>Updated SCIM user resource</returns>
    [HttpPut("Users/{id:guid}")]
    [ProducesResponseType(typeof(ScimUserResource), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ScimError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ScimError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ScimError), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ScimUserResource>> UpdateUser(Guid id, [FromBody] ScimUserResource user)
    {
        try
        {
            var updatedUser = await _scimService.UpdateUserAsync(id, user);

            _logger.LogInformation("SCIM user updated: {UserId}", id);

            return Ok(updatedUser);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Failed to update SCIM user {UserId}: {Message}", id, ex.Message);

            if (ex.Message.Contains("not found"))
            {
                return NotFound(CreateScimError(
                    "resourceNotFound",
                    ex.Message,
                    StatusCodes.Status404NotFound
                ));
            }

            return BadRequest(CreateScimError(
                "invalidValue",
                ex.Message,
                StatusCodes.Status400BadRequest
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating SCIM user: {UserId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, CreateScimError(
                "internalServerError",
                "An unexpected error occurred while updating the user",
                StatusCodes.Status500InternalServerError
            ));
        }
    }

    /// <summary>
    /// Partially update user (SCIM 2.0 PATCH)
    /// </summary>
    /// <param name="id">User ID</param>
    /// <param name="patchRequest">SCIM patch request</param>
    /// <returns>Updated SCIM user resource</returns>
    [HttpPatch("Users/{id:guid}")]
    [ProducesResponseType(typeof(ScimUserResource), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ScimError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ScimError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ScimError), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ScimUserResource>> PatchUser(Guid id, [FromBody] ScimPatchRequest patchRequest)
    {
        try
        {
            var updatedUser = await _scimService.PatchUserAsync(id, patchRequest);

            _logger.LogInformation("SCIM user patched: {UserId}", id);

            return Ok(updatedUser);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Failed to patch SCIM user {UserId}: {Message}", id, ex.Message);

            if (ex.Message.Contains("not found"))
            {
                return NotFound(CreateScimError(
                    "resourceNotFound",
                    ex.Message,
                    StatusCodes.Status404NotFound
                ));
            }

            return BadRequest(CreateScimError(
                "invalidValue",
                ex.Message,
                StatusCodes.Status400BadRequest
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error patching SCIM user: {UserId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, CreateScimError(
                "internalServerError",
                "An unexpected error occurred while patching the user",
                StatusCodes.Status500InternalServerError
            ));
        }
    }

    /// <summary>
    /// Delete user (SCIM 2.0)
    /// </summary>
    /// <param name="id">User ID</param>
    /// <returns>No content on success</returns>
    [HttpDelete("Users/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ScimError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ScimError), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteUser(Guid id)
    {
        try
        {
            await _scimService.DeleteUserAsync(id);

            _logger.LogInformation("SCIM user deleted: {UserId}", id);

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Failed to delete SCIM user {UserId}: {Message}", id, ex.Message);

            if (ex.Message.Contains("not found"))
            {
                return NotFound(CreateScimError(
                    "resourceNotFound",
                    ex.Message,
                    StatusCodes.Status404NotFound
                ));
            }

            return BadRequest(CreateScimError(
                "invalidValue",
                ex.Message,
                StatusCodes.Status400BadRequest
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting SCIM user: {UserId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, CreateScimError(
                "internalServerError",
                "An unexpected error occurred while deleting the user",
                StatusCodes.Status500InternalServerError
            ));
        }
    }

    #endregion

    #region Group Operations

    /// <summary>
    /// List groups (SCIM 2.0)
    /// </summary>
    /// <param name="filter">SCIM filter expression</param>
    /// <param name="startIndex">Starting index (1-based)</param>
    /// <param name="count">Number of results to return</param>
    /// <param name="attributes">Comma-separated list of attributes to return</param>
    /// <returns>SCIM list response with groups</returns>
    [HttpGet("Groups")]
    [ProducesResponseType(typeof(ScimListResponse<ScimGroupResource>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ScimError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ScimError), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ScimListResponse<ScimGroupResource>>> GetGroups(
        [FromQuery] string? filter = null,
        [FromQuery] int startIndex = 1,
        [FromQuery] int count = 20,
        [FromQuery] string? attributes = null)
    {
        try
        {
            if (startIndex < 1)
            {
                return BadRequest(CreateScimError(
                    "invalidValue",
                    "startIndex must be greater than or equal to 1",
                    StatusCodes.Status400BadRequest
                ));
            }

            if (count < 0 || count > 100)
            {
                return BadRequest(CreateScimError(
                    "invalidValue",
                    "count must be between 0 and 100",
                    StatusCodes.Status400BadRequest
                ));
            }

            var response = await _scimService.GetGroupsAsync(filter, startIndex, count, attributes);

            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Invalid SCIM filter: {Filter}, Error: {Message}", filter, ex.Message);
            return BadRequest(CreateScimError(
                "invalidFilter",
                ex.Message,
                StatusCodes.Status400BadRequest
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing SCIM groups");
            return StatusCode(StatusCodes.Status500InternalServerError, CreateScimError(
                "internalServerError",
                "An unexpected error occurred while listing groups",
                StatusCodes.Status500InternalServerError
            ));
        }
    }

    /// <summary>
    /// Get group by ID (SCIM 2.0)
    /// </summary>
    /// <param name="id">Group ID</param>
    /// <param name="attributes">Comma-separated list of attributes to return</param>
    /// <returns>SCIM group resource</returns>
    [HttpGet("Groups/{id:guid}")]
    [ProducesResponseType(typeof(ScimGroupResource), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ScimError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ScimError), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ScimGroupResource>> GetGroup(
        Guid id,
        [FromQuery] string? attributes = null)
    {
        try
        {
            var group = await _scimService.GetGroupByIdAsync(id, attributes);

            if (group == null)
            {
                return NotFound(CreateScimError(
                    "resourceNotFound",
                    $"Group with ID '{id}' was not found",
                    StatusCodes.Status404NotFound
                ));
            }

            return Ok(group);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving SCIM group: {GroupId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, CreateScimError(
                "internalServerError",
                "An unexpected error occurred while retrieving the group",
                StatusCodes.Status500InternalServerError
            ));
        }
    }

    #endregion

    private static ScimError CreateScimError(string scimType, string detail, int status)
    {
        return new ScimError
        {
            Schemas = new List<string> { "urn:ietf:params:scim:api:messages:2.0:Error" },
            ScimType = scimType,
            Detail = detail,
            Status = status
        };
    }
}
