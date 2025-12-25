using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using USP.Core.Models.DTOs.Session;
using USP.Core.Services.Session;

namespace USP.Api.Controllers.Session;

/// <summary>
/// Session management endpoints
/// </summary>
[ApiController]
[Route("api/v1/sessions")]
[Authorize]
public class SessionController : ControllerBase
{
    private readonly ISessionManagementService _sessionService;
    private readonly ILogger<SessionController> _logger;

    public SessionController(
        ISessionManagementService sessionService,
        ILogger<SessionController> logger)
    {
        _sessionService = sessionService;
        _logger = logger;
    }

    /// <summary>
    /// Get all active sessions for the current user
    /// </summary>
    /// <returns>List of active sessions</returns>
    [HttpGet]
    [ProducesResponseType(typeof(List<SessionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<SessionDto>>> GetActiveSessions()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
            {
                return Unauthorized(new { error = "User not authenticated" });
            }

            var sessions = await _sessionService.GetActiveSessionsAsync(userId);

            var currentSessionId = GetCurrentSessionId();
            foreach (var session in sessions)
            {
                session.IsCurrent = session.Id == currentSessionId;
            }

            return Ok(sessions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active sessions");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Get details of a specific session
    /// </summary>
    /// <param name="id">Session ID</param>
    /// <returns>Session details</returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(SessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<SessionDto>> GetSessionDetails(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
            {
                return Unauthorized(new { error = "User not authenticated" });
            }

            var session = await _sessionService.GetSessionDetailsAsync(id, userId);
            if (session == null)
            {
                return NotFound(new { error = "Session not found" });
            }

            var currentSessionId = GetCurrentSessionId();
            session.IsCurrent = session.Id == currentSessionId;

            return Ok(session);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving session {SessionId}", id);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Extend the expiration time of a session
    /// </summary>
    /// <param name="id">Session ID</param>
    /// <param name="request">Extension request</param>
    /// <returns>New expiration time</returns>
    [HttpPut("{id:guid}/extend")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult> ExtendSession(Guid id, [FromBody] ExtendSessionRequest? request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
            {
                return Unauthorized(new { error = "User not authenticated" });
            }

            var additionalMinutes = request?.AdditionalMinutes ?? 15;
            if (additionalMinutes < 1 || additionalMinutes > 120)
            {
                return BadRequest(new { error = "Additional minutes must be between 1 and 120" });
            }

            var newExpirationTime = await _sessionService.ExtendSessionAsync(id, userId, additionalMinutes);
            if (newExpirationTime == null)
            {
                return NotFound(new { error = "Session not found or expired" });
            }

            return Ok(new
            {
                sessionId = id,
                expiresAt = newExpirationTime,
                message = "Session extended successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extending session {SessionId}", id);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Revoke a specific session
    /// </summary>
    /// <param name="id">Session ID to revoke</param>
    /// <returns>Success response</returns>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult> RevokeSession(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
            {
                return Unauthorized(new { error = "User not authenticated" });
            }

            var currentSessionId = GetCurrentSessionId();
            if (id == currentSessionId)
            {
                return BadRequest(new { error = "Cannot revoke the current session. Use logout instead." });
            }

            var success = await _sessionService.RevokeSessionAsync(id, userId);
            if (!success)
            {
                return NotFound(new { error = "Session not found" });
            }

            return Ok(new { message = "Session revoked successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking session {SessionId}", id);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Revoke all sessions except the current one
    /// </summary>
    /// <returns>Number of sessions revoked</returns>
    [HttpDelete("others")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult> RevokeOtherSessions()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
            {
                return Unauthorized(new { error = "User not authenticated" });
            }

            var currentSessionId = GetCurrentSessionId();
            var revokedCount = await _sessionService.RevokeOtherSessionsAsync(userId, currentSessionId);

            return Ok(new
            {
                revokedCount,
                message = $"Successfully revoked {revokedCount} session(s)"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking other sessions");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Revoke all sessions for the current user (logout from all devices)
    /// </summary>
    /// <returns>Number of sessions revoked</returns>
    [HttpDelete("all")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult> RevokeAllSessions()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
            {
                return Unauthorized(new { error = "User not authenticated" });
            }

            var revokedCount = await _sessionService.RevokeAllSessionsAsync(userId);

            return Ok(new
            {
                revokedCount,
                message = $"Successfully revoked all {revokedCount} session(s). Please login again."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking all sessions");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Get count of active sessions for the current user
    /// </summary>
    /// <returns>Number of active sessions</returns>
    [HttpGet("count")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult> GetActiveSessionCount()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
            {
                return Unauthorized(new { error = "User not authenticated" });
            }

            var count = await _sessionService.GetActiveSessionCountAsync(userId);

            return Ok(new
            {
                userId,
                activeSessionCount = count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active session count");
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

    private Guid GetCurrentSessionId()
    {
        var sessionIdClaim = User.FindFirst("session_id")?.Value
            ?? User.FindFirst("sid")?.Value;

        if (string.IsNullOrEmpty(sessionIdClaim))
        {
            return Guid.Empty;
        }

        return Guid.TryParse(sessionIdClaim, out var sessionId) ? sessionId : Guid.Empty;
    }

    #endregion
}
