using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using USP.Core.Models.DTOs.PAM;
using USP.Core.Services.PAM;

namespace USP.Api.Controllers.PAM;

/// <summary>
/// Break-glass emergency access management
/// </summary>
[Authorize]
[ApiController]
[Route("api/v1/pam/break-glass")]
[Produces("application/json")]
public class BreakGlassController : ControllerBase
{
    private readonly IBreakGlassService _breakGlassService;
    private readonly ILogger<BreakGlassController> _logger;

    public BreakGlassController(
        IBreakGlassService breakGlassService,
        ILogger<BreakGlassController> logger)
    {
        _breakGlassService = breakGlassService;
        _logger = logger;
    }

    /// <summary>
    /// Activate break-glass emergency access
    /// </summary>
    [HttpPost("activate")]
    [ProducesResponseType(typeof(BreakGlassAccessDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<BreakGlassAccessDto>> ActivateBreakGlass([FromBody] ActivateBreakGlassRequest request)
    {
        try
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Unauthorized(new { error = "User not authenticated" });

            var access = await _breakGlassService.ActivateAsync(userId, request);

            _logger.LogWarning("CRITICAL: Break-glass emergency access activated by user {UserId}. Incident: {IncidentType}, Severity: {Severity}",
                userId, request.IncidentType, request.Severity);

            return Ok(access);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Break-glass activation failed for user {UserId}", GetUserId());
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error activating break-glass access");
            return StatusCode(500, new { error = "Failed to activate break-glass access" });
        }
    }

    /// <summary>
    /// Deactivate break-glass access
    /// </summary>
    [HttpPost("{accessId:guid}/deactivate")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<bool>> DeactivateBreakGlass(Guid accessId, [FromBody] DeactivateBreakGlassRequest request)
    {
        try
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Unauthorized(new { error = "User not authenticated" });

            var result = await _breakGlassService.DeactivateAsync(accessId, userId, request);

            if (!result)
                return NotFound(new { error = "Break-glass access not found or already deactivated" });

            _logger.LogInformation("Break-glass access {AccessId} deactivated by user {UserId}", accessId, userId);

            return Ok(new { success = true, message = "Break-glass access deactivated successfully" });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Break-glass deactivation failed for access {AccessId}", accessId);
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deactivating break-glass access {AccessId}", accessId);
            return StatusCode(500, new { error = "Failed to deactivate break-glass access" });
        }
    }

    /// <summary>
    /// Get break-glass access by ID
    /// </summary>
    [HttpGet("{accessId:guid}")]
    [ProducesResponseType(typeof(BreakGlassAccessDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<BreakGlassAccessDto>> GetAccessById(Guid accessId)
    {
        try
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Unauthorized(new { error = "User not authenticated" });

            var access = await _breakGlassService.GetAccessByIdAsync(accessId, userId);

            if (access == null)
                return NotFound(new { error = "Break-glass access not found" });

            return Ok(access);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving break-glass access {AccessId}", accessId);
            return StatusCode(500, new { error = "Failed to retrieve break-glass access" });
        }
    }

    /// <summary>
    /// Get active break-glass access for current user
    /// </summary>
    [HttpGet("active")]
    [ProducesResponseType(typeof(BreakGlassAccessDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<BreakGlassAccessDto>> GetActiveAccess()
    {
        try
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Unauthorized(new { error = "User not authenticated" });

            var access = await _breakGlassService.GetActiveAccessAsync(userId);

            if (access == null)
                return NotFound(new { error = "No active break-glass access found" });

            return Ok(access);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active break-glass access for user {UserId}", GetUserId());
            return StatusCode(500, new { error = "Failed to retrieve active break-glass access" });
        }
    }

    /// <summary>
    /// Get break-glass access history for current user
    /// </summary>
    [HttpGet("history")]
    [ProducesResponseType(typeof(List<BreakGlassAccessDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<BreakGlassAccessDto>>> GetUserHistory([FromQuery] int? limit = 50)
    {
        try
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Unauthorized(new { error = "User not authenticated" });

            var history = await _breakGlassService.GetUserHistoryAsync(userId, limit);
            return Ok(history);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving break-glass history for user {UserId}", GetUserId());
            return StatusCode(500, new { error = "Failed to retrieve break-glass history" });
        }
    }

    /// <summary>
    /// Get all break-glass access history (admin only)
    /// </summary>
    [HttpGet("history/all")]
    [Authorize(Roles = "Admin,SecurityAdmin")]
    [ProducesResponseType(typeof(List<BreakGlassAccessDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<BreakGlassAccessDto>>> GetAllHistory([FromQuery] int? limit = 100)
    {
        try
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Unauthorized(new { error = "User not authenticated" });

            var history = await _breakGlassService.GetAllHistoryAsync(userId, limit);
            return Ok(history);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all break-glass history");
            return StatusCode(500, new { error = "Failed to retrieve break-glass history" });
        }
    }

    /// <summary>
    /// Get break-glass accesses pending review
    /// </summary>
    [HttpGet("pending-review")]
    [Authorize(Roles = "Admin,SecurityAdmin")]
    [ProducesResponseType(typeof(List<BreakGlassAccessDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<BreakGlassAccessDto>>> GetPendingReview()
    {
        try
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Unauthorized(new { error = "User not authenticated" });

            var pending = await _breakGlassService.GetPendingReviewAsync(userId);
            return Ok(pending);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving pending break-glass reviews");
            return StatusCode(500, new { error = "Failed to retrieve pending reviews" });
        }
    }

    /// <summary>
    /// Review a break-glass access
    /// </summary>
    [HttpPost("{accessId:guid}/review")]
    [Authorize(Roles = "Admin,SecurityAdmin")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<bool>> ReviewAccess(Guid accessId, [FromBody] ReviewBreakGlassRequest request)
    {
        try
        {
            var reviewerId = GetUserId();
            if (reviewerId == Guid.Empty)
                return Unauthorized(new { error = "User not authenticated" });

            var result = await _breakGlassService.ReviewAccessAsync(accessId, reviewerId, request);

            if (!result)
                return NotFound(new { error = "Break-glass access not found or already reviewed" });

            _logger.LogInformation("Break-glass access {AccessId} reviewed by {ReviewerId}. Decision: {Decision}",
                accessId, reviewerId, request.ReviewDecision);

            return Ok(new { success = true, message = "Break-glass access reviewed successfully" });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Break-glass review failed for access {AccessId}", accessId);
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reviewing break-glass access {AccessId}", accessId);
            return StatusCode(500, new { error = "Failed to review break-glass access" });
        }
    }

    /// <summary>
    /// Get break-glass access statistics
    /// </summary>
    [HttpGet("statistics")]
    [Authorize(Roles = "Admin,SecurityAdmin")]
    [ProducesResponseType(typeof(BreakGlassStatisticsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<BreakGlassStatisticsDto>> GetStatistics()
    {
        try
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Unauthorized(new { error = "User not authenticated" });

            var statistics = await _breakGlassService.GetStatisticsAsync(userId);
            return Ok(statistics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving break-glass statistics");
            return StatusCode(500, new { error = "Failed to retrieve break-glass statistics" });
        }
    }

    // Policy Management Endpoints

    /// <summary>
    /// Get active break-glass policy
    /// </summary>
    [HttpGet("policy")]
    [ProducesResponseType(typeof(BreakGlassPolicyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<BreakGlassPolicyDto>> GetActivePolicy()
    {
        try
        {
            var policy = await _breakGlassService.GetActivePolicyAsync();

            if (policy == null)
                return NotFound(new { error = "No active break-glass policy found" });

            return Ok(policy);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active break-glass policy");
            return StatusCode(500, new { error = "Failed to retrieve active policy" });
        }
    }

    /// <summary>
    /// Create break-glass policy (admin only)
    /// </summary>
    [HttpPost("policy")]
    [Authorize(Roles = "Admin,SecurityAdmin")]
    [ProducesResponseType(typeof(BreakGlassPolicyDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<BreakGlassPolicyDto>> CreatePolicy([FromBody] CreateBreakGlassPolicyRequest request)
    {
        try
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Unauthorized(new { error = "User not authenticated" });

            var policy = await _breakGlassService.CreatePolicyAsync(userId, request);

            _logger.LogInformation("Break-glass policy {PolicyId} created by user {UserId}", policy.Id, userId);

            return CreatedAtAction(nameof(GetActivePolicy), new { id = policy.Id }, policy);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Break-glass policy creation failed");
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating break-glass policy");
            return StatusCode(500, new { error = "Failed to create break-glass policy" });
        }
    }

    /// <summary>
    /// Update break-glass policy (admin only)
    /// </summary>
    [HttpPut("policy/{policyId:guid}")]
    [Authorize(Roles = "Admin,SecurityAdmin")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<bool>> UpdatePolicy(Guid policyId, [FromBody] CreateBreakGlassPolicyRequest request)
    {
        try
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Unauthorized(new { error = "User not authenticated" });

            var result = await _breakGlassService.UpdatePolicyAsync(policyId, userId, request);

            if (!result)
                return NotFound(new { error = "Break-glass policy not found" });

            _logger.LogInformation("Break-glass policy {PolicyId} updated by user {UserId}", policyId, userId);

            return Ok(new { success = true, message = "Break-glass policy updated successfully" });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Break-glass policy update failed for policy {PolicyId}", policyId);
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating break-glass policy {PolicyId}", policyId);
            return StatusCode(500, new { error = "Failed to update break-glass policy" });
        }
    }

    /// <summary>
    /// Get all break-glass policies (admin only)
    /// </summary>
    [HttpGet("policies")]
    [Authorize(Roles = "Admin,SecurityAdmin")]
    [ProducesResponseType(typeof(List<BreakGlassPolicyDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<BreakGlassPolicyDto>>> GetPolicies()
    {
        try
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Unauthorized(new { error = "User not authenticated" });

            var policies = await _breakGlassService.GetPoliciesAsync(userId);
            return Ok(policies);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving break-glass policies");
            return StatusCode(500, new { error = "Failed to retrieve break-glass policies" });
        }
    }

    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
    }
}
