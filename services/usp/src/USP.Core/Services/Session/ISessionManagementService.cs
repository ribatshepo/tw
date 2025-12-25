using USP.Core.Models.DTOs.Session;

namespace USP.Core.Services.Session;

/// <summary>
/// Session management service for handling user session lifecycle
/// </summary>
public interface ISessionManagementService
{
    /// <summary>
    /// Get all active sessions for a user
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <returns>List of active sessions</returns>
    Task<List<SessionDto>> GetActiveSessionsAsync(Guid userId);

    /// <summary>
    /// Get session details by ID
    /// </summary>
    /// <param name="sessionId">Session ID</param>
    /// <param name="userId">User ID (for authorization)</param>
    /// <returns>Session details or null if not found</returns>
    Task<SessionDto?> GetSessionDetailsAsync(Guid sessionId, Guid userId);

    /// <summary>
    /// Create a new session
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="tokenHash">Hashed access token</param>
    /// <param name="refreshTokenHash">Hashed refresh token</param>
    /// <param name="ipAddress">Client IP address</param>
    /// <param name="userAgent">Client user agent</param>
    /// <param name="expiresAt">Session expiration time</param>
    /// <returns>Created session ID</returns>
    Task<Guid> CreateSessionAsync(
        Guid userId,
        string tokenHash,
        string? refreshTokenHash,
        string ipAddress,
        string? userAgent,
        DateTime expiresAt);

    /// <summary>
    /// Update session activity timestamp
    /// </summary>
    /// <param name="sessionId">Session ID</param>
    /// <returns>True if updated successfully</returns>
    Task<bool> UpdateSessionActivityAsync(Guid sessionId);

    /// <summary>
    /// Extend session expiration
    /// </summary>
    /// <param name="sessionId">Session ID</param>
    /// <param name="userId">User ID (for authorization)</param>
    /// <param name="additionalMinutes">Additional minutes to extend (default: 15)</param>
    /// <returns>New expiration time or null if failed</returns>
    Task<DateTime?> ExtendSessionAsync(Guid sessionId, Guid userId, int additionalMinutes = 15);

    /// <summary>
    /// Revoke a specific session
    /// </summary>
    /// <param name="sessionId">Session ID to revoke</param>
    /// <param name="userId">User ID (for authorization)</param>
    /// <returns>True if revoked successfully</returns>
    Task<bool> RevokeSessionAsync(Guid sessionId, Guid userId);

    /// <summary>
    /// Revoke all sessions for a user except the current one
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="currentSessionId">Current session ID to keep active</param>
    /// <returns>Number of sessions revoked</returns>
    Task<int> RevokeOtherSessionsAsync(Guid userId, Guid currentSessionId);

    /// <summary>
    /// Revoke all sessions for a user
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <returns>Number of sessions revoked</returns>
    Task<int> RevokeAllSessionsAsync(Guid userId);

    /// <summary>
    /// Clean up expired sessions (background task)
    /// </summary>
    /// <returns>Number of sessions cleaned up</returns>
    Task<int> CleanupExpiredSessionsAsync();

    /// <summary>
    /// Get session count for a user
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <returns>Number of active sessions</returns>
    Task<int> GetActiveSessionCountAsync(Guid userId);

    /// <summary>
    /// Validate session exists and is active
    /// </summary>
    /// <param name="sessionId">Session ID</param>
    /// <param name="userId">User ID</param>
    /// <returns>True if session is valid and active</returns>
    Task<bool> ValidateSessionAsync(Guid sessionId, Guid userId);
}
