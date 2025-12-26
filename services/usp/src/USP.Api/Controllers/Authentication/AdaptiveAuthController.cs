using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using USP.Core.Models.DTOs.Authentication;
using USP.Core.Services.Authentication;

namespace USP.Api.Controllers.Authentication;

/// <summary>
/// Manages adaptive authentication policies and step-up challenges
/// Provides risk-based authentication with dynamic MFA requirements
/// </summary>
[ApiController]
[Route("api/v1/auth/adaptive")]
[Authorize]
public class AdaptiveAuthController : ControllerBase
{
    private readonly IAdaptiveAuthPolicyEngine _policyEngine;
    private readonly ILogger<AdaptiveAuthController> _logger;

    public AdaptiveAuthController(
        IAdaptiveAuthPolicyEngine policyEngine,
        ILogger<AdaptiveAuthController> logger)
    {
        _policyEngine = policyEngine;
        _logger = logger;
    }

    /// <summary>
    /// Evaluate authentication policy for current request
    /// Determines if step-up authentication is required based on risk
    /// </summary>
    /// <param name="resourcePath">Resource being accessed</param>
    /// <param name="riskScore">Current risk score (optional, will calculate if not provided)</param>
    /// <returns>Policy evaluation result</returns>
    [HttpPost("evaluate")]
    [ProducesResponseType(typeof(PolicyEvaluationResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PolicyEvaluationResultDto>> EvaluatePolicy(
        [FromQuery] string? resourcePath = null,
        [FromQuery] int? riskScore = null)
    {
        try
        {
            var userId = GetUserId();

            // Use provided risk score or default to medium (50)
            var risk = riskScore ?? 50;

            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var userAgent = Request.Headers.UserAgent.ToString();
            var deviceFingerprint = Request.Headers["X-Device-Fingerprint"].FirstOrDefault();

            var result = await _policyEngine.EvaluatePolicyAsync(
                userId,
                risk,
                resourcePath,
                ipAddress,
                userAgent,
                deviceFingerprint);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating adaptive auth policy");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Initiate step-up authentication challenge
    /// Creates a challenge session that user must complete
    /// </summary>
    /// <param name="request">Step-up initiation request</param>
    /// <returns>Challenge details with session token</returns>
    [HttpPost("step-up/initiate")]
    [ProducesResponseType(typeof(StepUpChallengeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<StepUpChallengeDto>> InitiateStepUp([FromBody] InitiateStepUpDto request)
    {
        try
        {
            var userId = GetUserId();

            // Evaluate policy to determine required factors
            var evaluation = await _policyEngine.EvaluatePolicyAsync(
                userId,
                50, // Default risk score
                request.ResourcePath);

            if (evaluation.Action != "step_up")
            {
                return BadRequest(new { error = "Step-up not required for this request" });
            }

            var challenge = await _policyEngine.InitiateStepUpAsync(
                userId,
                evaluation.RequiredFactors,
                request.ResourcePath,
                evaluation.StepUpValidityMinutes);

            return Ok(challenge);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initiating step-up challenge");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Validate a step-up authentication factor
    /// User submits credential for verification
    /// </summary>
    /// <param name="request">Factor validation request</param>
    /// <returns>Validation result</returns>
    [HttpPost("step-up/validate")]
    [ProducesResponseType(typeof(StepUpValidationResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<StepUpValidationResultDto>> ValidateStepUpFactor([FromBody] ValidateStepUpFactorDto request)
    {
        try
        {
            var userId = GetUserId();

            var result = await _policyEngine.ValidateStepUpFactorAsync(
                request.SessionToken,
                userId,
                request.Factor,
                request.Credential);

            if (!result.IsValid)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating step-up factor");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Complete step-up authentication session
    /// Marks session as completed if all factors verified
    /// </summary>
    /// <param name="request">Completion request</param>
    /// <returns>Completion result</returns>
    [HttpPost("step-up/complete")]
    [ProducesResponseType(typeof(StepUpCompletionResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<StepUpCompletionResultDto>> CompleteStepUp([FromBody] CompleteStepUpDto request)
    {
        try
        {
            var userId = GetUserId();

            var result = await _policyEngine.CompleteStepUpAsync(
                request.SessionToken,
                userId);

            if (!result.IsCompleted)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing step-up authentication");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Check if user has valid step-up session for resource
    /// Used to bypass step-up if recently completed
    /// </summary>
    /// <param name="resourcePath">Resource path</param>
    /// <returns>True if valid session exists</returns>
    [HttpGet("step-up/status")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<bool>> GetStepUpStatus([FromQuery] string? resourcePath = null)
    {
        try
        {
            var userId = GetUserId();

            var hasValidSession = await _policyEngine.HasValidStepUpSessionAsync(userId, resourcePath);

            return Ok(hasValidSession);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking step-up status");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Get authentication events for current user
    /// Returns audit trail of authentication activities
    /// </summary>
    /// <param name="eventType">Filter by event type</param>
    /// <param name="startDate">Start date</param>
    /// <param name="endDate">End date</param>
    /// <param name="limit">Maximum results</param>
    /// <returns>List of authentication events</returns>
    [HttpGet("events")]
    [ProducesResponseType(typeof(List<AuthenticationEventDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<AuthenticationEventDto>>> GetAuthenticationEvents(
        [FromQuery] string? eventType = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] int limit = 100)
    {
        try
        {
            var userId = GetUserId();

            var events = await _policyEngine.GetAuthenticationEventsAsync(
                userId,
                eventType,
                startDate,
                endDate,
                limit);

            return Ok(events);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving authentication events");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Get authentication statistics for current user
    /// </summary>
    /// <param name="days">Number of days to include</param>
    /// <returns>Authentication statistics</returns>
    [HttpGet("statistics")]
    [ProducesResponseType(typeof(AuthenticationStatisticsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthenticationStatisticsDto>> GetStatistics([FromQuery] int days = 30)
    {
        try
        {
            var userId = GetUserId();

            var stats = await _policyEngine.GetAuthenticationStatisticsAsync(userId, days);

            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving authentication statistics");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    // ====================================================================================
    // Policy Management Endpoints (Admin)
    // ====================================================================================

    /// <summary>
    /// Create or update adaptive authentication policy
    /// Admin endpoint for policy management
    /// </summary>
    /// <param name="request">Policy configuration</param>
    /// <returns>Created/updated policy</returns>
    [HttpPost("policies")]
    [Authorize(Roles = "Admin,SecurityAdmin")]
    [ProducesResponseType(typeof(AdaptiveAuthPolicyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AdaptiveAuthPolicyDto>> CreateOrUpdatePolicy([FromBody] CreateAdaptiveAuthPolicyDto request)
    {
        try
        {
            var policy = await _policyEngine.CreateOrUpdatePolicyAsync(request);

            return Ok(policy);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid policy operation");
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating/updating policy");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Get policy by ID
    /// </summary>
    /// <param name="policyId">Policy ID</param>
    /// <returns>Policy details</returns>
    [HttpGet("policies/{policyId:guid}")]
    [Authorize(Roles = "Admin,SecurityAdmin")]
    [ProducesResponseType(typeof(AdaptiveAuthPolicyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdaptiveAuthPolicyDto>> GetPolicy(Guid policyId)
    {
        try
        {
            var policy = await _policyEngine.GetPolicyAsync(policyId);

            return Ok(policy);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Policy {PolicyId} not found", policyId);
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving policy {PolicyId}", policyId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Get all active policies
    /// </summary>
    /// <returns>List of active policies</returns>
    [HttpGet("policies")]
    [Authorize(Roles = "Admin,SecurityAdmin")]
    [ProducesResponseType(typeof(List<AdaptiveAuthPolicyDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<List<AdaptiveAuthPolicyDto>>> GetActivePolicies()
    {
        try
        {
            var policies = await _policyEngine.GetActivePoliciesAsync();

            return Ok(policies);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active policies");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Delete policy
    /// </summary>
    /// <param name="policyId">Policy ID</param>
    [HttpDelete("policies/{policyId:guid}")]
    [Authorize(Roles = "Admin,SecurityAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeletePolicy(Guid policyId)
    {
        try
        {
            await _policyEngine.DeletePolicyAsync(policyId);

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Policy {PolicyId} not found", policyId);
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting policy {PolicyId}", policyId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
    }
}
