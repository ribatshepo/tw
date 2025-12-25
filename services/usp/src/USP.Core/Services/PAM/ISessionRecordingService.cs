using USP.Core.Models.DTOs.PAM;

namespace USP.Core.Services.PAM;

/// <summary>
/// Service for recording and managing privileged sessions
/// </summary>
public interface ISessionRecordingService
{
    /// <summary>
    /// Start a new session recording
    /// </summary>
    Task<SessionRecordingDto> StartSessionAsync(Guid checkoutId, Guid userId, StartSessionRequest request);

    /// <summary>
    /// End a session recording
    /// </summary>
    Task<bool> EndSessionAsync(Guid sessionId, Guid userId);

    /// <summary>
    /// Record a command/query in a session
    /// </summary>
    Task<SessionCommandDto> RecordCommandAsync(Guid sessionId, RecordCommandRequest request);

    /// <summary>
    /// Get session by ID
    /// </summary>
    Task<SessionRecordingDto?> GetSessionByIdAsync(Guid sessionId, Guid userId);

    /// <summary>
    /// Get sessions for a user
    /// </summary>
    Task<List<SessionRecordingDto>> GetUserSessionsAsync(Guid userId, int? limit = 50);

    /// <summary>
    /// Get sessions for an account
    /// </summary>
    Task<List<SessionRecordingDto>> GetAccountSessionsAsync(Guid accountId, Guid userId, int? limit = 50);

    /// <summary>
    /// Get active sessions
    /// </summary>
    Task<List<SessionRecordingDto>> GetActiveSessionsAsync(Guid userId);

    /// <summary>
    /// Get session commands (query log)
    /// </summary>
    Task<List<SessionCommandDto>> GetSessionCommandsAsync(Guid sessionId, Guid userId, int? limit = 100);

    /// <summary>
    /// Terminate a session (admin operation)
    /// </summary>
    Task<bool> TerminateSessionAsync(Guid sessionId, Guid adminUserId, string reason);

    /// <summary>
    /// Get sessions with suspicious activity
    /// </summary>
    Task<List<SessionRecordingDto>> GetSuspiciousSessionsAsync(Guid userId);

    /// <summary>
    /// Get session statistics
    /// </summary>
    Task<SessionStatisticsDto> GetSessionStatisticsAsync(Guid userId);

    /// <summary>
    /// Search session commands
    /// </summary>
    Task<List<SessionCommandDto>> SearchCommandsAsync(Guid userId, string searchTerm, int? limit = 50);
}
