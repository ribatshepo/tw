using USP.Core.Domain.Entities.Identity;

namespace USP.Core.Interfaces.Repositories;

/// <summary>
/// Repository interface for session CRUD operations.
/// Sessions are stored in both Redis (for fast access) and PostgreSQL (for audit).
/// </summary>
public interface ISessionRepository
{
    /// <summary>
    /// Stores a session in Redis with TTL and PostgreSQL for audit.
    /// </summary>
    /// <param name="session">The session to store</param>
    /// <param name="accessTokenTtl">TTL for access token in seconds (default: 3600 = 60 minutes)</param>
    /// <param name="refreshTokenTtl">TTL for refresh token in seconds (default: 604800 = 7 days)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task StoreAsync(
        Session session,
        int accessTokenTtl = 3600,
        int refreshTokenTtl = 604800,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a session by ID from Redis (fast path) or PostgreSQL (fallback).
    /// </summary>
    /// <param name="sessionId">The session ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The session if found, null otherwise</returns>
    Task<Session?> GetByIdAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a session by refresh token from Redis.
    /// </summary>
    /// <param name="refreshToken">The refresh token</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The session if found, null otherwise</returns>
    Task<Session?> GetByRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all active sessions for a user from Redis and PostgreSQL.
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of active sessions</returns>
    Task<List<Session>> GetActiveSessionsByUserIdAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes a session (deletes from Redis and marks as revoked in PostgreSQL).
    /// </summary>
    /// <param name="sessionId">The session ID to revoke</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if revoked successfully, false if session not found</returns>
    Task<bool> RevokeAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes all sessions for a user.
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of sessions revoked</returns>
    Task<int> RevokeAllByUserIdAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the last activity timestamp for a session in Redis.
    /// </summary>
    /// <param name="sessionId">The session ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task UpdateLastActivityAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts active sessions for a user.
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of active sessions</returns>
    Task<int> CountActiveSessionsByUserIdAsync(string userId, CancellationToken cancellationToken = default);
}
