using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using USP.Core.Models.DTOs.PAM;
using USP.Core.Services.PAM;

namespace USP.Api.Controllers.PAM;

/// <summary>
/// Manages privileged session recording for audit and compliance
/// </summary>
[ApiController]
[Route("api/v1/pam/sessions")]
[Authorize]
public class SessionController : ControllerBase
{
    private readonly ISessionRecordingService _sessionService;
    private readonly ILogger<SessionController> _logger;

    public SessionController(
        ISessionRecordingService sessionService,
        ILogger<SessionController> logger)
    {
        _sessionService = sessionService;
        _logger = logger;
    }

    /// <summary>
    /// Start a new privileged session recording
    /// </summary>
    [HttpPost("{checkoutId:guid}/start")]
    [ProducesResponseType(typeof(SessionRecordingDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SessionRecordingDto>> StartSession(
        Guid checkoutId,
        [FromBody] StartSessionRequest request)
    {
        try
        {
            var userId = GetUserId();
            var session = await _sessionService.StartSessionAsync(checkoutId, userId, request);

            return CreatedAtAction(
                nameof(GetSessionById),
                new { sessionId = session.Id },
                session);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting session for checkout {CheckoutId}", checkoutId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// End a session recording
    /// </summary>
    [HttpPost("{sessionId:guid}/end")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> EndSession(Guid sessionId)
    {
        try
        {
            var userId = GetUserId();
            var result = await _sessionService.EndSessionAsync(sessionId, userId);

            if (!result)
                return NotFound(new { error = "Session not found or access denied" });

            return Ok(new { message = "Session ended successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ending session {SessionId}", sessionId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Record a command/query in a session
    /// </summary>
    [HttpPost("{sessionId:guid}/commands")]
    [ProducesResponseType(typeof(SessionCommandDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SessionCommandDto>> RecordCommand(
        Guid sessionId,
        [FromBody] RecordCommandRequest request)
    {
        try
        {
            var command = await _sessionService.RecordCommandAsync(sessionId, request);
            return CreatedAtAction(nameof(RecordCommand), new { sessionId }, command);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording command for session {SessionId}", sessionId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Get session by ID
    /// </summary>
    [HttpGet("{sessionId:guid}")]
    [ProducesResponseType(typeof(SessionRecordingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SessionRecordingDto>> GetSessionById(Guid sessionId)
    {
        try
        {
            var userId = GetUserId();
            var session = await _sessionService.GetSessionByIdAsync(sessionId, userId);

            if (session == null)
                return NotFound(new { error = "Session not found" });

            return Ok(session);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving session {SessionId}", sessionId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Get sessions for current user
    /// </summary>
    [HttpGet("my")]
    [ProducesResponseType(typeof(List<SessionRecordingDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<SessionRecordingDto>>> GetMySessions([FromQuery] int? limit = 50)
    {
        try
        {
            var userId = GetUserId();
            var sessions = await _sessionService.GetUserSessionsAsync(userId, limit);
            return Ok(sessions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user sessions");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Get sessions for a specific account
    /// </summary>
    [HttpGet("account/{accountId:guid}")]
    [ProducesResponseType(typeof(List<SessionRecordingDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<SessionRecordingDto>>> GetAccountSessions(
        Guid accountId,
        [FromQuery] int? limit = 50)
    {
        try
        {
            var userId = GetUserId();
            var sessions = await _sessionService.GetAccountSessionsAsync(accountId, userId, limit);
            return Ok(sessions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving account sessions for {AccountId}", accountId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Get all active sessions
    /// </summary>
    [HttpGet("active")]
    [ProducesResponseType(typeof(List<SessionRecordingDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<SessionRecordingDto>>> GetActiveSessions()
    {
        try
        {
            var userId = GetUserId();
            var sessions = await _sessionService.GetActiveSessionsAsync(userId);
            return Ok(sessions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active sessions");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Get commands for a session
    /// </summary>
    [HttpGet("{sessionId:guid}/commands")]
    [ProducesResponseType(typeof(List<SessionCommandDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<SessionCommandDto>>> GetSessionCommands(
        Guid sessionId,
        [FromQuery] int? limit = 100)
    {
        try
        {
            var userId = GetUserId();
            var commands = await _sessionService.GetSessionCommandsAsync(sessionId, userId, limit);
            return Ok(commands);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving commands for session {SessionId}", sessionId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Terminate a session (admin operation)
    /// </summary>
    [HttpPost("{sessionId:guid}/terminate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> TerminateSession(
        Guid sessionId,
        [FromBody] TerminateSessionRequest request)
    {
        try
        {
            var userId = GetUserId();
            var result = await _sessionService.TerminateSessionAsync(sessionId, userId, request.Reason);

            if (!result)
                return NotFound(new { error = "Session not found or access denied" });

            return Ok(new { message = "Session terminated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error terminating session {SessionId}", sessionId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Get sessions with suspicious activity
    /// </summary>
    [HttpGet("suspicious")]
    [ProducesResponseType(typeof(List<SessionRecordingDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<SessionRecordingDto>>> GetSuspiciousSessions()
    {
        try
        {
            var userId = GetUserId();
            var sessions = await _sessionService.GetSuspiciousSessionsAsync(userId);
            return Ok(sessions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving suspicious sessions");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Get session statistics
    /// </summary>
    [HttpGet("statistics")]
    [ProducesResponseType(typeof(SessionStatisticsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SessionStatisticsDto>> GetStatistics()
    {
        try
        {
            var userId = GetUserId();
            var statistics = await _sessionService.GetSessionStatisticsAsync(userId);
            return Ok(statistics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving session statistics");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Search commands across all accessible sessions
    /// </summary>
    [HttpGet("commands/search")]
    [ProducesResponseType(typeof(List<SessionCommandDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<SessionCommandDto>>> SearchCommands(
        [FromQuery] string searchTerm,
        [FromQuery] int? limit = 50)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return BadRequest(new { error = "Search term is required" });

            var userId = GetUserId();
            var commands = await _sessionService.SearchCommandsAsync(userId, searchTerm, limit);
            return Ok(commands);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching commands");
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
/// Request to terminate a session
/// </summary>
public class TerminateSessionRequest
{
    public string Reason { get; set; } = string.Empty;
}
