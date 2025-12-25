using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using USP.Core.Models.DTOs.PAM;
using USP.Core.Services.PAM;

namespace USP.Api.Controllers.PAM;

/// <summary>
/// Controller for dual control and approval workflows
/// </summary>
[ApiController]
[Route("api/v1/pam/approvals")]
[Authorize]
public class ApprovalController : ControllerBase
{
    private readonly IDualControlService _dualControlService;
    private readonly ILogger<ApprovalController> _logger;

    public ApprovalController(
        IDualControlService dualControlService,
        ILogger<ApprovalController> logger)
    {
        _dualControlService = dualControlService;
        _logger = logger;
    }

    /// <summary>
    /// Create an approval request
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(AccessApprovalDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AccessApprovalDto>> CreateApprovalRequest(
        [FromBody] CreateApprovalRequest request)
    {
        try
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Unauthorized(new { error = "User ID not found in token" });

            var approval = await _dualControlService.CreateApprovalRequestAsync(request, userId);

            return CreatedAtAction(nameof(GetApprovalById), new { id = approval.Id }, approval);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating approval request");
            return StatusCode(500, new { error = "Failed to create approval request" });
        }
    }

    /// <summary>
    /// Approve an approval request
    /// </summary>
    [HttpPost("{id:guid}/approve")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> Approve(
        Guid id,
        [FromBody] ApprovalActionRequest? request = null)
    {
        try
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Unauthorized(new { error = "User ID not found in token" });

            var success = await _dualControlService.ApproveAsync(id, userId, request?.Notes);

            if (!success)
                return NotFound(new { error = "Approval not found" });

            return Ok(new { message = "Approval granted successfully" });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving approval {ApprovalId}", id);
            return StatusCode(500, new { error = "Failed to approve" });
        }
    }

    /// <summary>
    /// Deny an approval request
    /// </summary>
    [HttpPost("{id:guid}/deny")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> Deny(
        Guid id,
        [FromBody] DenyApprovalRequest request)
    {
        try
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Unauthorized(new { error = "User ID not found in token" });

            if (string.IsNullOrWhiteSpace(request.Reason))
                return BadRequest(new { error = "Reason is required for denial" });

            var success = await _dualControlService.DenyAsync(id, userId, request.Reason);

            if (!success)
                return NotFound(new { error = "Approval not found" });

            return Ok(new { message = "Approval denied successfully" });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error denying approval {ApprovalId}", id);
            return StatusCode(500, new { error = "Failed to deny" });
        }
    }

    /// <summary>
    /// Get pending approvals for current user (as approver)
    /// </summary>
    [HttpGet("pending")]
    [ProducesResponseType(typeof(List<AccessApprovalDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<AccessApprovalDto>>> GetPendingApprovals()
    {
        try
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Unauthorized(new { error = "User ID not found in token" });

            var approvals = await _dualControlService.GetPendingApprovalsAsync(userId);

            return Ok(approvals);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving pending approvals");
            return StatusCode(500, new { error = "Failed to retrieve pending approvals" });
        }
    }

    /// <summary>
    /// Get approval requests created by current user
    /// </summary>
    [HttpGet("my-requests")]
    [ProducesResponseType(typeof(List<AccessApprovalDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<AccessApprovalDto>>> GetMyRequests()
    {
        try
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Unauthorized(new { error = "User ID not found in token" });

            var approvals = await _dualControlService.GetMyRequestsAsync(userId);

            return Ok(approvals);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user's approval requests");
            return StatusCode(500, new { error = "Failed to retrieve requests" });
        }
    }

    /// <summary>
    /// Get approval by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AccessApprovalDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AccessApprovalDto>> GetApprovalById(Guid id)
    {
        try
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Unauthorized(new { error = "User ID not found in token" });

            var approval = await _dualControlService.GetApprovalByIdAsync(id, userId);

            if (approval == null)
                return NotFound(new { error = "Approval not found or access denied" });

            return Ok(approval);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving approval {ApprovalId}", id);
            return StatusCode(500, new { error = "Failed to retrieve approval" });
        }
    }

    /// <summary>
    /// Cancel an approval request
    /// </summary>
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> CancelApproval(Guid id)
    {
        try
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Unauthorized(new { error = "User ID not found in token" });

            var success = await _dualControlService.CancelApprovalAsync(id, userId);

            if (!success)
                return NotFound(new { error = "Approval not found" });

            return Ok(new { message = "Approval cancelled successfully" });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling approval {ApprovalId}", id);
            return StatusCode(500, new { error = "Failed to cancel approval" });
        }
    }

    /// <summary>
    /// Get approval statistics for current user
    /// </summary>
    [HttpGet("statistics")]
    [ProducesResponseType(typeof(ApprovalStatisticsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApprovalStatisticsDto>> GetStatistics()
    {
        try
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Unauthorized(new { error = "User ID not found in token" });

            var stats = await _dualControlService.GetApprovalStatisticsAsync(userId);

            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving approval statistics");
            return StatusCode(500, new { error = "Failed to retrieve statistics" });
        }
    }

    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
    }
}

// Request DTOs
public class ApprovalActionRequest
{
    public string? Notes { get; set; }
}

public class DenyApprovalRequest
{
    public string Reason { get; set; } = string.Empty;
}
