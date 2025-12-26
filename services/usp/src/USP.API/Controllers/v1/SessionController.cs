using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using USP.API.DTOs.Session;
using USP.Core.Interfaces.Services.Authentication;

namespace USP.API.Controllers.v1;

/// <summary>
/// Controller for managing user sessions.
/// </summary>
[ApiController]
[Route("api/v1/sessions")]
[Authorize]
public class SessionController : ControllerBase
{
    private readonly ISessionService _sessionService;

    public SessionController(ISessionService sessionService)
    {
        _sessionService = sessionService;
    }

    /// <summary>
    /// Lists all active sessions for the current user.
    /// </summary>
    /// <returns>List of active sessions</returns>
    [HttpGet]
    [ProducesResponseType(typeof(ActiveSessionsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ListActiveSessions(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var sessions = await _sessionService.GetActiveSessionsAsync(userId, cancellationToken);

        var response = new ActiveSessionsResponse
        {
            Sessions = sessions.Select(s => new SessionResponse
            {
                Id = s.Id,
                UserId = s.UserId,
                IpAddress = s.IpAddress,
                UserAgent = s.UserAgent ?? string.Empty,
                DeviceFingerprint = s.DeviceFingerprint,
                DeviceInfo = ParseUserAgent(s.UserAgent ?? string.Empty),
                CreatedAt = s.CreatedAt,
                LastActivityAt = s.LastActivityAt,
                ExpiresAt = s.ExpiresAt,
                IsRevoked = s.IsRevoked,
                RevokedAt = s.RevokedAt
            }).ToList(),
            TotalCount = sessions.Count,
            CurrentSessionId = GetCurrentSessionId()
        };

        return Ok(response);
    }

    /// <summary>
    /// Gets details of a specific session.
    /// </summary>
    /// <param name="id">Session ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Session details</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(SessionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSessionById(string id, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var session = await _sessionService.GetSessionAsync(id, cancellationToken);

        if (session == null)
        {
            return NotFound(new { message = "Session not found" });
        }

        // Ensure user can only view their own sessions
        if (session.UserId != userId)
        {
            return NotFound(new { message = "Session not found" });
        }

        var response = new SessionResponse
        {
            Id = session.Id,
            UserId = session.UserId,
            IpAddress = session.IpAddress,
            UserAgent = session.UserAgent ?? string.Empty,
            DeviceFingerprint = session.DeviceFingerprint,
            DeviceInfo = ParseUserAgent(session.UserAgent ?? string.Empty),
            CreatedAt = session.CreatedAt,
            LastActivityAt = session.LastActivityAt,
            ExpiresAt = session.ExpiresAt,
            IsRevoked = session.IsRevoked,
            RevokedAt = session.RevokedAt
        };

        return Ok(response);
    }

    /// <summary>
    /// Revokes a specific session.
    /// </summary>
    /// <param name="id">Session ID to revoke</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success confirmation</returns>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevokeSession(string id, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        // Verify session belongs to user
        var session = await _sessionService.GetSessionAsync(id, cancellationToken);
        if (session == null || session.UserId != userId)
        {
            return NotFound(new { message = "Session not found" });
        }

        var revoked = await _sessionService.RevokeSessionAsync(id, cancellationToken);

        if (!revoked)
        {
            return NotFound(new { message = "Session not found" });
        }

        return Ok(new { message = "Session revoked successfully", sessionId = id });
    }

    /// <summary>
    /// Revokes all sessions for the current user.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Count of revoked sessions</returns>
    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RevokeAllSessions(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var count = await _sessionService.RevokeAllSessionsAsync(userId, cancellationToken);

        return Ok(new
        {
            message = "All sessions revoked successfully",
            revokedCount = count
        });
    }

    private string ParseUserAgent(string userAgent)
    {
        // Simple user agent parsing (can be enhanced with a library like UAParser)
        if (string.IsNullOrEmpty(userAgent))
        {
            return "Unknown Device";
        }

        if (userAgent.Contains("iPhone"))
        {
            return "iPhone";
        }

        if (userAgent.Contains("iPad"))
        {
            return "iPad";
        }

        if (userAgent.Contains("Android"))
        {
            return "Android Device";
        }

        if (userAgent.Contains("Windows"))
        {
            return "Windows PC";
        }

        if (userAgent.Contains("Mac"))
        {
            return "Mac";
        }

        if (userAgent.Contains("Linux"))
        {
            return "Linux PC";
        }

        return "Unknown Device";
    }

    private string? GetCurrentSessionId()
    {
        // Extract session ID from claims (if available)
        return User.FindFirstValue("session_id");
    }
}
