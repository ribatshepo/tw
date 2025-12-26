using USP.Core.Models.DTOs.PAM;

namespace USP.Core.Services.PAM;

/// <summary>
/// Service for replaying and analyzing recorded privileged sessions
/// Provides timeline reconstruction, search, and export for compliance audits
/// </summary>
public interface ISessionPlaybackService
{
    /// <summary>
    /// Get playback timeline for a session
    /// Returns all commands in chronological order with timing information
    /// </summary>
    /// <param name="sessionId">Session identifier</param>
    /// <param name="userId">User requesting playback (for access control)</param>
    /// <returns>Timeline with all commands and their execution context</returns>
    Task<SessionPlaybackTimelineDto> GetPlaybackTimelineAsync(Guid sessionId, Guid userId);

    /// <summary>
    /// Get session state at a specific point in time
    /// Returns all commands executed up to the specified timestamp
    /// </summary>
    /// <param name="sessionId">Session identifier</param>
    /// <param name="userId">User requesting playback</param>
    /// <param name="timestamp">Point in time to reconstruct session state</param>
    /// <returns>Session frame showing state at specified time</returns>
    Task<SessionPlaybackFrameDto> GetPlaybackFrameAsync(Guid sessionId, Guid userId, TimeSpan timestamp);

    /// <summary>
    /// Search within a specific session's commands and responses
    /// </summary>
    /// <param name="sessionId">Session identifier</param>
    /// <param name="userId">User requesting search</param>
    /// <param name="searchTerm">Text to search for</param>
    /// <param name="options">Search options (case sensitivity, regex, etc.)</param>
    /// <returns>Search results with context</returns>
    Task<SessionPlaybackSearchResultDto> SearchPlaybackAsync(
        Guid sessionId,
        Guid userId,
        string searchTerm,
        PlaybackSearchOptions? options = null);

    /// <summary>
    /// Export session playback in various formats for compliance reporting
    /// </summary>
    /// <param name="sessionId">Session identifier</param>
    /// <param name="userId">User requesting export</param>
    /// <param name="format">Export format (json, csv, html, pdf, text)</param>
    /// <returns>Exported session data</returns>
    Task<SessionPlaybackExportDto> ExportSessionAsync(
        Guid sessionId,
        Guid userId,
        PlaybackExportFormat format);

    /// <summary>
    /// Get playback metadata without full command list
    /// Useful for UI to show session duration, command count, etc.
    /// </summary>
    /// <param name="sessionId">Session identifier</param>
    /// <param name="userId">User requesting metadata</param>
    /// <returns>Playback metadata</returns>
    Task<SessionPlaybackMetadataDto> GetPlaybackMetadataAsync(Guid sessionId, Guid userId);

    /// <summary>
    /// Get multiple sessions' playback summaries for a user or account
    /// Used for compliance dashboards
    /// </summary>
    /// <param name="userId">User requesting summaries</param>
    /// <param name="accountId">Optional filter by account</param>
    /// <param name="startDate">Optional start date filter</param>
    /// <param name="endDate">Optional end date filter</param>
    /// <param name="limit">Maximum number of results</param>
    /// <returns>List of playback summaries</returns>
    Task<List<SessionPlaybackSummaryDto>> GetPlaybackSummariesAsync(
        Guid userId,
        Guid? accountId = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        int limit = 50);
}
