using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using USP.Core.Models.DTOs.PAM;
using USP.Core.Services.PAM;

namespace USP.Api.Controllers.PAM;

/// <summary>
/// Provides session playback and analysis capabilities for compliance auditing
/// Supports timeline reconstruction, search, and multiple export formats
/// </summary>
[ApiController]
[Route("api/v1/pam/playback")]
[Authorize]
public class PlaybackController : ControllerBase
{
    private readonly ISessionPlaybackService _playbackService;
    private readonly ILogger<PlaybackController> _logger;

    public PlaybackController(
        ISessionPlaybackService playbackService,
        ILogger<PlaybackController> logger)
    {
        _playbackService = playbackService;
        _logger = logger;
    }

    /// <summary>
    /// Get complete playback timeline for a session
    /// Returns all commands in chronological order with timing information
    /// </summary>
    /// <param name="sessionId">Session identifier</param>
    /// <returns>Complete timeline with all commands and metadata</returns>
    [HttpGet("{sessionId:guid}/timeline")]
    [ProducesResponseType(typeof(SessionPlaybackTimelineDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<SessionPlaybackTimelineDto>> GetTimeline(Guid sessionId)
    {
        try
        {
            var userId = GetUserId();
            var timeline = await _playbackService.GetPlaybackTimelineAsync(sessionId, userId);
            return Ok(timeline);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Session {SessionId} not found", sessionId);
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Access denied to session {SessionId}", sessionId);
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting timeline for session {SessionId}", sessionId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Get session state at a specific point in time
    /// Returns all commands executed up to the specified timestamp
    /// </summary>
    /// <param name="sessionId">Session identifier</param>
    /// <param name="timestampSeconds">Relative timestamp in seconds from session start</param>
    /// <returns>Session frame showing state at specified time</returns>
    [HttpGet("{sessionId:guid}/frame")]
    [ProducesResponseType(typeof(SessionPlaybackFrameDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<SessionPlaybackFrameDto>> GetFrame(
        Guid sessionId,
        [FromQuery] double timestampSeconds)
    {
        try
        {
            if (timestampSeconds < 0)
            {
                return BadRequest(new { error = "Timestamp must be non-negative" });
            }

            var userId = GetUserId();
            var timestamp = TimeSpan.FromSeconds(timestampSeconds);
            var frame = await _playbackService.GetPlaybackFrameAsync(sessionId, userId, timestamp);
            return Ok(frame);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Session {SessionId} not found", sessionId);
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Access denied to session {SessionId}", sessionId);
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting frame for session {SessionId} at {Timestamp}s",
                sessionId, timestampSeconds);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Search within a session's commands and responses
    /// Supports literal and regex search with configurable options
    /// </summary>
    /// <param name="sessionId">Session identifier</param>
    /// <param name="searchTerm">Text or regex pattern to search for</param>
    /// <param name="caseSensitive">Enable case-sensitive search (default: false)</param>
    /// <param name="useRegex">Use regex pattern matching (default: false)</param>
    /// <param name="searchCommands">Search in command text (default: true)</param>
    /// <param name="searchResponses">Search in response text (default: true)</param>
    /// <param name="searchErrorMessages">Search in error messages (default: true)</param>
    /// <param name="contextCharacters">Number of context characters around match (default: 100)</param>
    /// <returns>Search results with context</returns>
    [HttpGet("{sessionId:guid}/search")]
    [ProducesResponseType(typeof(SessionPlaybackSearchResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<SessionPlaybackSearchResultDto>> Search(
        Guid sessionId,
        [FromQuery] string searchTerm,
        [FromQuery] bool caseSensitive = false,
        [FromQuery] bool useRegex = false,
        [FromQuery] bool searchCommands = true,
        [FromQuery] bool searchResponses = true,
        [FromQuery] bool searchErrorMessages = true,
        [FromQuery] int contextCharacters = 100)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                return BadRequest(new { error = "Search term is required" });
            }

            if (contextCharacters < 0 || contextCharacters > 1000)
            {
                return BadRequest(new { error = "Context characters must be between 0 and 1000" });
            }

            var userId = GetUserId();
            var options = new PlaybackSearchOptions
            {
                CaseSensitive = caseSensitive,
                UseRegex = useRegex,
                SearchCommands = searchCommands,
                SearchResponses = searchResponses,
                SearchErrorMessages = searchErrorMessages,
                ContextCharacters = contextCharacters
            };

            var result = await _playbackService.SearchPlaybackAsync(sessionId, userId, searchTerm, options);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Session {SessionId} not found", sessionId);
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Access denied to session {SessionId}", sessionId);
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching session {SessionId}", sessionId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Export session playback in various formats for compliance reporting
    /// Supports JSON, CSV, HTML, and plain text formats
    /// </summary>
    /// <param name="sessionId">Session identifier</param>
    /// <param name="format">Export format (json, csv, html, text)</param>
    /// <returns>Exported session data as file download</returns>
    [HttpGet("{sessionId:guid}/export")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Export(
        Guid sessionId,
        [FromQuery] string format = "json")
    {
        try
        {
            if (!Enum.TryParse<PlaybackExportFormat>(format, true, out var exportFormat))
            {
                return BadRequest(new { error = $"Invalid export format. Supported formats: json, csv, html, text" });
            }

            var userId = GetUserId();
            var export = await _playbackService.ExportSessionAsync(sessionId, userId, exportFormat);

            return File(export.Data, export.MimeType, export.FileName);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Session {SessionId} not found", sessionId);
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Access denied to session {SessionId}", sessionId);
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting session {SessionId} as {Format}",
                sessionId, format);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Get playback metadata without full command list
    /// Useful for UI to show session duration, command count, statistics, etc.
    /// </summary>
    /// <param name="sessionId">Session identifier</param>
    /// <returns>Playback metadata and statistics</returns>
    [HttpGet("{sessionId:guid}/metadata")]
    [ProducesResponseType(typeof(SessionPlaybackMetadataDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<SessionPlaybackMetadataDto>> GetMetadata(Guid sessionId)
    {
        try
        {
            var userId = GetUserId();
            var metadata = await _playbackService.GetPlaybackMetadataAsync(sessionId, userId);
            return Ok(metadata);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Session {SessionId} not found", sessionId);
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Access denied to session {SessionId}", sessionId);
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting metadata for session {SessionId}", sessionId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Get multiple sessions' playback summaries for a user or account
    /// Used for compliance dashboards and session listing
    /// </summary>
    /// <param name="accountId">Optional filter by account</param>
    /// <param name="startDate">Optional start date filter (ISO 8601 format)</param>
    /// <param name="endDate">Optional end date filter (ISO 8601 format)</param>
    /// <param name="limit">Maximum number of results (default: 50, max: 500)</param>
    /// <returns>List of playback summaries</returns>
    [HttpGet("summaries")]
    [ProducesResponseType(typeof(List<SessionPlaybackSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<List<SessionPlaybackSummaryDto>>> GetSummaries(
        [FromQuery] Guid? accountId = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] int limit = 50)
    {
        try
        {
            if (limit < 1 || limit > 500)
            {
                return BadRequest(new { error = "Limit must be between 1 and 500" });
            }

            if (startDate.HasValue && endDate.HasValue && startDate > endDate)
            {
                return BadRequest(new { error = "Start date must be before end date" });
            }

            var userId = GetUserId();
            var summaries = await _playbackService.GetPlaybackSummariesAsync(
                userId,
                accountId,
                startDate,
                endDate,
                limit);

            return Ok(summaries);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting playback summaries");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
    }
}
