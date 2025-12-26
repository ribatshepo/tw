using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using USP.Core.Models.DTOs.Workspace;
using USP.Core.Services.Workspace;

namespace USP.Api.Controllers.Workspace;

/// <summary>
/// Controller for workspace and multi-tenancy management
/// </summary>
[ApiController]
[Route("api/v1/workspaces")]
[Authorize]
[Produces("application/json")]
public class WorkspaceController : ControllerBase
{
    private readonly IWorkspaceService _workspaceService;
    private readonly ILogger<WorkspaceController> _logger;

    public WorkspaceController(
        IWorkspaceService workspaceService,
        ILogger<WorkspaceController> logger)
    {
        _workspaceService = workspaceService;
        _logger = logger;
    }

    /// <summary>
    /// Create a new workspace
    /// </summary>
    /// <param name="request">Workspace creation request</param>
    /// <returns>Created workspace information</returns>
    [HttpPost]
    [ProducesResponseType(typeof(WorkspaceDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<WorkspaceDto>> CreateWorkspace([FromBody] CreateWorkspaceRequest request)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            var workspace = await _workspaceService.CreateWorkspaceAsync(request, currentUserId);

            _logger.LogInformation("Workspace created: {WorkspaceId}, {Name}", workspace.Id, workspace.Name);

            return CreatedAtAction(nameof(GetWorkspace), new { id = workspace.Id }, workspace);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Failed to create workspace: {Message}", ex.Message);
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Workspace creation failed",
                Detail = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating workspace: {Name}", request.Name);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal server error",
                Detail = "An unexpected error occurred while creating the workspace"
            });
        }
    }

    /// <summary>
    /// Get workspace by ID
    /// </summary>
    /// <param name="id">Workspace ID</param>
    /// <returns>Workspace information</returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(WorkspaceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<WorkspaceDto>> GetWorkspace(Guid id)
    {
        try
        {
            var workspace = await _workspaceService.GetWorkspaceAsync(id);

            if (workspace == null)
            {
                return NotFound(new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Workspace not found",
                    Detail = $"Workspace with ID '{id}' was not found"
                });
            }

            return Ok(workspace);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving workspace: {WorkspaceId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal server error",
                Detail = "An unexpected error occurred while retrieving the workspace"
            });
        }
    }

    /// <summary>
    /// List workspaces for the current user
    /// </summary>
    /// <returns>List of workspaces</returns>
    [HttpGet]
    [ProducesResponseType(typeof(List<WorkspaceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<WorkspaceDto>>> ListWorkspaces()
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            var workspaces = await _workspaceService.GetUserWorkspacesAsync(currentUserId);

            return Ok(workspaces);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing workspaces for user");
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal server error",
                Detail = "An unexpected error occurred while listing workspaces"
            });
        }
    }

    /// <summary>
    /// Add member to workspace
    /// </summary>
    /// <param name="id">Workspace ID</param>
    /// <param name="request">Member addition request</param>
    /// <returns>Added workspace member information</returns>
    [HttpPost("{id:guid}/members")]
    [ProducesResponseType(typeof(WorkspaceMemberDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<WorkspaceMemberDto>> AddMember(Guid id, [FromBody] AddWorkspaceMemberRequest request)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            var member = await _workspaceService.AddMemberAsync(id, request, currentUserId);

            _logger.LogInformation("Member added to workspace: WorkspaceId={WorkspaceId}, UserId={UserId}, Role={Role}",
                id, request.UserId, request.Role);

            return CreatedAtAction(
                nameof(GetWorkspace),
                new { id },
                member
            );
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Failed to add member to workspace {WorkspaceId}: {Message}", id, ex.Message);

            if (ex.Message.Contains("not found"))
            {
                return NotFound(new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Workspace or user not found",
                    Detail = ex.Message
                });
            }

            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Add member failed",
                Detail = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding member to workspace: {WorkspaceId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal server error",
                Detail = "An unexpected error occurred while adding the member"
            });
        }
    }

    /// <summary>
    /// Remove member from workspace
    /// </summary>
    /// <param name="id">Workspace ID</param>
    /// <param name="userId">User ID to remove</param>
    /// <returns>No content on success</returns>
    [HttpDelete("{id:guid}/members/{userId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> RemoveMember(Guid id, Guid userId)
    {
        try
        {
            await _workspaceService.RemoveMemberAsync(id, userId);

            _logger.LogInformation("Member removed from workspace: WorkspaceId={WorkspaceId}, UserId={UserId}",
                id, userId);

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Failed to remove member from workspace {WorkspaceId}: {Message}", id, ex.Message);

            if (ex.Message.Contains("not found"))
            {
                return NotFound(new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Workspace or member not found",
                    Detail = ex.Message
                });
            }

            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Remove member failed",
                Detail = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing member from workspace: WorkspaceId={WorkspaceId}, UserId={UserId}",
                id, userId);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal server error",
                Detail = "An unexpected error occurred while removing the member"
            });
        }
    }

    /// <summary>
    /// Get workspace members
    /// </summary>
    /// <param name="id">Workspace ID</param>
    /// <returns>List of workspace members</returns>
    [HttpGet("{id:guid}/members")]
    [ProducesResponseType(typeof(List<WorkspaceMemberDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<WorkspaceMemberDto>>> GetMembers(Guid id)
    {
        try
        {
            var members = await _workspaceService.GetMembersAsync(id);

            return Ok(members);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Failed to get members for workspace {WorkspaceId}: {Message}", id, ex.Message);

            if (ex.Message.Contains("not found"))
            {
                return NotFound(new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Workspace not found",
                    Detail = ex.Message
                });
            }

            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Get members failed",
                Detail = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting members for workspace: {WorkspaceId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal server error",
                Detail = "An unexpected error occurred while getting workspace members"
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
