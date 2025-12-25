using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using USP.Core.Models.DTOs.PAM;
using USP.Core.Services.PAM;

namespace USP.Api.Controllers.PAM;

/// <summary>
/// Manages Just-In-Time (JIT) access for temporary privilege elevation
/// </summary>
[ApiController]
[Route("api/v1/pam/jit")]
[Authorize]
public class JitController : ControllerBase
{
    private readonly IJitAccessService _jitService;
    private readonly ILogger<JitController> _logger;

    public JitController(
        IJitAccessService jitService,
        ILogger<JitController> logger)
    {
        _jitService = jitService;
        _logger = logger;
    }

    /// <summary>
    /// Request a new JIT access grant
    /// </summary>
    [HttpPost("request")]
    [ProducesResponseType(typeof(JitAccessDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<JitAccessDto>> RequestAccess([FromBody] RequestJitAccessRequest request)
    {
        try
        {
            var userId = GetUserId();
            var access = await _jitService.RequestAccessAsync(userId, request);

            return CreatedAtAction(
                nameof(GetAccessById),
                new { accessId = access.Id },
                access);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error requesting JIT access");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Get JIT access grant by ID
    /// </summary>
    [HttpGet("{accessId:guid}")]
    [ProducesResponseType(typeof(JitAccessDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<JitAccessDto>> GetAccessById(Guid accessId)
    {
        try
        {
            var userId = GetUserId();
            var access = await _jitService.GetAccessByIdAsync(accessId, userId);

            if (access == null)
                return NotFound(new { error = "Access grant not found" });

            return Ok(access);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving JIT access {AccessId}", accessId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Get active JIT access grants for current user
    /// </summary>
    [HttpGet("active")]
    [ProducesResponseType(typeof(List<JitAccessDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<JitAccessDto>>> GetActiveGrants()
    {
        try
        {
            var userId = GetUserId();
            var grants = await _jitService.GetActiveAccessGrantsAsync(userId);
            return Ok(grants);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active JIT access grants");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Get all JIT access grants for current user
    /// </summary>
    [HttpGet("my")]
    [ProducesResponseType(typeof(List<JitAccessDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<JitAccessDto>>> GetMyGrants([FromQuery] int? limit = 50)
    {
        try
        {
            var userId = GetUserId();
            var grants = await _jitService.GetUserAccessGrantsAsync(userId, limit);
            return Ok(grants);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user JIT access grants");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Get pending JIT access requests awaiting approval
    /// </summary>
    [HttpGet("pending")]
    [ProducesResponseType(typeof(List<JitAccessDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<JitAccessDto>>> GetPendingRequests()
    {
        try
        {
            var userId = GetUserId();
            var requests = await _jitService.GetPendingRequestsAsync(userId);
            return Ok(requests);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving pending JIT access requests");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Revoke a JIT access grant
    /// </summary>
    [HttpDelete("{accessId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> RevokeAccess(Guid accessId, [FromBody] RevokeJitAccessRequest request)
    {
        try
        {
            var userId = GetUserId();
            var result = await _jitService.RevokeAccessAsync(accessId, userId, request.Reason);

            if (!result)
                return NotFound(new { error = "Access grant not found or access denied" });

            return Ok(new { message = "JIT access revoked successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking JIT access {AccessId}", accessId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Approve a JIT access request
    /// </summary>
    [HttpPost("{accessId:guid}/approve")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> ApproveAccess(Guid accessId)
    {
        try
        {
            var userId = GetUserId();
            var result = await _jitService.ApproveAccessAsync(accessId, userId);

            if (!result)
                return NotFound(new { error = "Access request not found or access denied" });

            return Ok(new { message = "JIT access approved successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving JIT access {AccessId}", accessId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Deny a JIT access request
    /// </summary>
    [HttpPost("{accessId:guid}/deny")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DenyAccess(Guid accessId, [FromBody] RevokeJitAccessRequest request)
    {
        try
        {
            var userId = GetUserId();
            var result = await _jitService.DenyAccessAsync(accessId, userId, request.Reason);

            if (!result)
                return NotFound(new { error = "Access request not found or access denied" });

            return Ok(new { message = "JIT access denied successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error denying JIT access {AccessId}", accessId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Get JIT access statistics
    /// </summary>
    [HttpGet("statistics")]
    [ProducesResponseType(typeof(JitAccessStatisticsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<JitAccessStatisticsDto>> GetStatistics()
    {
        try
        {
            var userId = GetUserId();
            var statistics = await _jitService.GetStatisticsAsync(userId);
            return Ok(statistics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving JIT access statistics");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    // Template Management Endpoints

    /// <summary>
    /// Create a new JIT access template
    /// </summary>
    [HttpPost("templates")]
    [ProducesResponseType(typeof(JitAccessTemplateDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<JitAccessTemplateDto>> CreateTemplate([FromBody] CreateJitTemplateRequest request)
    {
        try
        {
            var userId = GetUserId();
            var template = await _jitService.CreateTemplateAsync(userId, request);

            return CreatedAtAction(
                nameof(GetTemplateById),
                new { templateId = template.Id },
                template);
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(403, new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating JIT access template");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Get JIT access template by ID
    /// </summary>
    [HttpGet("templates/{templateId:guid}")]
    [ProducesResponseType(typeof(JitAccessTemplateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<JitAccessTemplateDto>> GetTemplateById(Guid templateId)
    {
        try
        {
            var template = await _jitService.GetTemplateByIdAsync(templateId);

            if (template == null)
                return NotFound(new { error = "Template not found" });

            return Ok(template);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving JIT access template {TemplateId}", templateId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Get all JIT access templates
    /// </summary>
    [HttpGet("templates")]
    [ProducesResponseType(typeof(List<JitAccessTemplateDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<JitAccessTemplateDto>>> GetTemplates()
    {
        try
        {
            var userId = GetUserId();
            var templates = await _jitService.GetTemplatesAsync(userId);
            return Ok(templates);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving JIT access templates");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Update a JIT access template
    /// </summary>
    [HttpPut("templates/{templateId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> UpdateTemplate(Guid templateId, [FromBody] CreateJitTemplateRequest request)
    {
        try
        {
            var userId = GetUserId();
            var result = await _jitService.UpdateTemplateAsync(templateId, userId, request);

            if (!result)
                return NotFound(new { error = "Template not found or access denied" });

            return Ok(new { message = "Template updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating JIT access template {TemplateId}", templateId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Delete a JIT access template
    /// </summary>
    [HttpDelete("templates/{templateId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> DeleteTemplate(Guid templateId)
    {
        try
        {
            var userId = GetUserId();
            var result = await _jitService.DeleteTemplateAsync(templateId, userId);

            if (!result)
                return NotFound(new { error = "Template not found or access denied" });

            return Ok(new { message = "Template deleted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting JIT access template {TemplateId}", templateId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Activate or deactivate a JIT access template
    /// </summary>
    [HttpPatch("templates/{templateId:guid}/toggle")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> ToggleTemplateActive(Guid templateId, [FromBody] ToggleTemplateRequest request)
    {
        try
        {
            var userId = GetUserId();
            var result = await _jitService.ToggleTemplateActiveAsync(templateId, userId, request.Active);

            if (!result)
                return NotFound(new { error = "Template not found or access denied" });

            return Ok(new { message = $"Template {(request.Active ? "activated" : "deactivated")} successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling JIT access template {TemplateId}", templateId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
    }
}

/// <summary>
/// Request to toggle template active status
/// </summary>
public class ToggleTemplateRequest
{
    public bool Active { get; set; }
}
