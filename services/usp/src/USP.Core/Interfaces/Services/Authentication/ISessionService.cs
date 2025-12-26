using USP.Core.Domain.Entities.Identity;

namespace USP.Core.Interfaces.Services.Authentication;

/// <summary>
/// Provides session creation, retrieval, revocation, and cleanup operations.
/// Sessions are stored in both Redis (for fast access) and PostgreSQL (for audit).
/// </summary>
public interface ISessionService
{
    /// <summary>
    /// Creates a new session for a user.
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="refreshToken">The refresh token</param>
    /// <param name="ipAddress">The client's IP address</param>
    /// <param name="userAgent">The client's user agent</param>
    /// <param name="deviceFingerprint">Optional device fingerprint</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The created session</returns>
    Task<Session> CreateSessionAsync(
        string userId,
        string refreshToken,
        string ipAddress,
        string userAgent,
        string? deviceFingerprint = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a session by ID from Redis or PostgreSQL.
    /// </summary>
    /// <param name="sessionId">The session ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The session if found, null otherwise</returns>
    Task<Session?> GetSessionAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a session by refresh token.
    /// </summary>
    /// <param name="refreshToken">The refresh token</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The session if found, null otherwise</returns>
    Task<Session?> GetSessionByRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all active sessions for a user.
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of active sessions</returns>
    Task<List<Session>> GetActiveSessionsAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes a session (deletes from Redis and marks as revoked in PostgreSQL).
    /// </summary>
    /// <param name="sessionId">The session ID to revoke</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if revoked successfully, false if session not found</returns>
    Task<bool> RevokeSessionAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes all sessions for a user.
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of sessions revoked</returns>
    Task<int> RevokeAllSessionsAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the last activity timestamp for a session.
    /// </summary>
    /// <param name="sessionId">The session ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task UpdateSessionActivityAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Enforces concurrent session limits for a user.
    /// Revokes oldest sessions if limit is exceeded.
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="maxConcurrentSessions">Maximum allowed concurrent sessions</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task EnforceConcurrentSessionLimitAsync(
        string userId,
        int maxConcurrentSessions,
        CancellationToken cancellationToken = default);
}
