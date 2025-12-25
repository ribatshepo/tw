using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using USP.Core.Models.DTOs.Authentication;
using USP.Core.Services.Authentication;

namespace USP.Api.Controllers.Risk;

/// <summary>
/// Risk assessment and user risk profile management
/// </summary>
[ApiController]
[Route("api/v1/risk")]
[Authorize]
public class RiskController : ControllerBase
{
    private readonly IRiskAssessmentService _riskService;
    private readonly ILogger<RiskController> _logger;

    public RiskController(
        IRiskAssessmentService riskService,
        ILogger<RiskController> logger)
    {
        _riskService = riskService;
        _logger = logger;
    }

    /// <summary>
    /// Get current user's risk score
    /// </summary>
    /// <returns>Risk score (0-100)</returns>
    [HttpGet("score")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult> GetCurrentUserRiskScore()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
            {
                return Unauthorized(new { error = "User not authenticated" });
            }

            var score = await _riskService.GetUserRiskScoreAsync(userId);

            return Ok(new
            {
                userId,
                riskScore = score,
                riskLevel = score switch
                {
                    >= 85 => "critical",
                    >= 60 => "high",
                    >= 30 => "medium",
                    _ => "low"
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting current user risk score");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Get detailed risk assessment for current user
    /// </summary>
    /// <returns>Comprehensive risk assessment</returns>
    [HttpGet("assessment")]
    [ProducesResponseType(typeof(RiskAssessmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<RiskAssessmentResponse>> GetCurrentUserAssessment()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
            {
                return Unauthorized(new { error = "User not authenticated" });
            }

            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var userAgent = HttpContext.Request.Headers.UserAgent.ToString();

            var request = new RiskAssessmentRequest
            {
                UserId = userId,
                IpAddress = ipAddress,
                UserAgent = userAgent
            };

            var assessment = await _riskService.AssessRiskAsync(request);

            return Ok(assessment);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting current user assessment");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Get risk assessment history for current user
    /// </summary>
    /// <param name="limit">Number of records to return (default: 50, max: 100)</param>
    /// <returns>List of risk assessments</returns>
    [HttpGet("history")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult> GetRiskHistory([FromQuery] int limit = 50)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
            {
                return Unauthorized(new { error = "User not authenticated" });
            }

            limit = Math.Min(limit, 100);

            var history = await _riskService.GetRiskHistoryAsync(userId, limit);

            return Ok(new
            {
                userId,
                count = history.Count,
                history = history.Select(h => new
                {
                    h.Id,
                    h.RiskLevel,
                    h.RiskScore,
                    h.RiskFactors,
                    h.Action,
                    h.AssessedAt
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting risk history");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Get list of high-risk users (admin only)
    /// </summary>
    /// <param name="minimumScore">Minimum risk score threshold (default: 70)</param>
    /// <returns>List of high-risk user profiles</returns>
    [HttpGet("users/high-risk")]
    [Authorize(Roles = "admin,security_admin")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> GetHighRiskUsers([FromQuery] int minimumScore = 70)
    {
        try
        {
            minimumScore = Math.Max(0, Math.Min(100, minimumScore));

            var highRiskUsers = await _riskService.GetHighRiskUsersAsync(minimumScore);

            return Ok(new
            {
                minimumScore,
                count = highRiskUsers.Count,
                users = highRiskUsers.Select(u => new
                {
                    u.UserId,
                    u.CurrentRiskScore,
                    u.RiskTier,
                    u.IsCompromised,
                    u.ConsecutiveFailedLogins,
                    u.SuspiciousActivityCount,
                    u.LastSuspiciousActivity,
                    u.UpdatedAt
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting high-risk users");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Manually adjust a user's risk score (admin only)
    /// </summary>
    /// <param name="id">User ID</param>
    /// <param name="request">Adjustment request</param>
    /// <returns>Success response</returns>
    [HttpPost("users/{id:guid}/adjust")]
    [Authorize(Roles = "admin,security_admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> AdjustUserRiskScore(Guid id, [FromBody] AdjustRiskScoreRequest request)
    {
        try
        {
            var adminUserId = GetCurrentUserId();
            if (adminUserId == Guid.Empty)
            {
                return Unauthorized(new { error = "User not authenticated" });
            }

            if (request.NewScore < 0 || request.NewScore > 100)
            {
                return BadRequest(new { error = "Risk score must be between 0 and 100" });
            }

            if (string.IsNullOrWhiteSpace(request.Reason))
            {
                return BadRequest(new { error = "Reason is required" });
            }

            await _riskService.AdjustUserRiskScoreAsync(id, request.NewScore, request.Reason, adminUserId);

            return Ok(new
            {
                userId = id,
                newScore = request.NewScore,
                adjustedBy = adminUserId,
                reason = request.Reason,
                message = "Risk score adjusted successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adjusting risk score for user {UserId}", id);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Mark a user account as compromised (admin only)
    /// </summary>
    /// <param name="id">User ID</param>
    /// <param name="request">Compromise details</param>
    /// <returns>Success response</returns>
    [HttpPost("users/{id:guid}/mark-compromised")]
    [Authorize(Roles = "admin,security_admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> MarkAccountCompromised(Guid id, [FromBody] MarkCompromisedRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Reason))
            {
                return BadRequest(new { error = "Reason is required" });
            }

            await _riskService.MarkAccountCompromisedAsync(id, request.Reason);

            return Ok(new
            {
                userId = id,
                isCompromised = true,
                reason = request.Reason,
                message = "Account marked as compromised successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking account as compromised for user {UserId}", id);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Clear compromised flag after password reset (admin only)
    /// </summary>
    /// <param name="id">User ID</param>
    /// <returns>Success response</returns>
    [HttpPost("users/{id:guid}/clear-compromised")]
    [Authorize(Roles = "admin,security_admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> ClearCompromisedFlag(Guid id)
    {
        try
        {
            await _riskService.ClearCompromisedFlagAsync(id);

            return Ok(new
            {
                userId = id,
                isCompromised = false,
                message = "Compromised flag cleared successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing compromised flag for user {UserId}", id);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    #region Private Helper Methods

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value
            ?? User.FindFirst("user_id")?.Value;

        if (string.IsNullOrEmpty(userIdClaim))
        {
            return Guid.Empty;
        }

        return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
    }

    #endregion
}

/// <summary>
/// Request to adjust user's risk score
/// </summary>
public class AdjustRiskScoreRequest
{
    public int NewScore { get; set; }
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// Request to mark account as compromised
/// </summary>
public class MarkCompromisedRequest
{
    public string Reason { get; set; } = string.Empty;
}
