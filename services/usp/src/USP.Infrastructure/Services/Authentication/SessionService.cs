using Microsoft.Extensions.Options;
using USP.Core.Domain.Entities.Identity;
using USP.Core.Interfaces.Repositories;
using USP.Core.Interfaces.Services.Authentication;
using USP.Shared.Configuration.Options;

namespace USP.Infrastructure.Services.Authentication;

/// <summary>
/// Provides session management operations with Redis-backed storage.
/// </summary>
public class SessionService : ISessionService
{
    private readonly ISessionRepository _sessionRepository;
    private readonly JwtOptions _jwtOptions;

    public SessionService(ISessionRepository sessionRepository, IOptions<JwtOptions> jwtOptions)
    {
        _sessionRepository = sessionRepository;
        _jwtOptions = jwtOptions.Value;
    }

    /// <summary>
    /// Creates a new session for a user.
    /// </summary>
    public async Task<Session> CreateSessionAsync(
        string userId,
        string refreshToken,
        string ipAddress,
        string userAgent,
        string? deviceFingerprint = null,
        CancellationToken cancellationToken = default)
    {
        var session = new Session
        {
            Id = Guid.NewGuid().ToString(),
            UserId = userId,
            RefreshToken = refreshToken,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            DeviceFingerprint = deviceFingerprint,
            CreatedAt = DateTime.UtcNow,
            LastActivityAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(_jwtOptions.RefreshTokenExpirationDays)
        };

        // Store in Redis and PostgreSQL
        var accessTokenTtl = _jwtOptions.ExpirationMinutes * 60; // Convert to seconds
        var refreshTokenTtl = _jwtOptions.RefreshTokenExpirationDays * 24 * 60 * 60; // Convert to seconds
        await _sessionRepository.StoreAsync(session, accessTokenTtl, refreshTokenTtl, cancellationToken);

        return session;
    }

    /// <summary>
    /// Gets a session by ID from Redis or PostgreSQL.
    /// </summary>
    public async Task<Session?> GetSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        return await _sessionRepository.GetByIdAsync(sessionId, cancellationToken);
    }

    /// <summary>
    /// Gets a session by refresh token.
    /// </summary>
    public async Task<Session?> GetSessionByRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        return await _sessionRepository.GetByRefreshTokenAsync(refreshToken, cancellationToken);
    }

    /// <summary>
    /// Gets all active sessions for a user.
    /// </summary>
    public async Task<List<Session>> GetActiveSessionsAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _sessionRepository.GetActiveSessionsByUserIdAsync(userId, cancellationToken);
    }

    /// <summary>
    /// Revokes a session (deletes from Redis and marks as revoked in PostgreSQL).
    /// </summary>
    public async Task<bool> RevokeSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        return await _sessionRepository.RevokeAsync(sessionId, cancellationToken);
    }

    /// <summary>
    /// Revokes all sessions for a user.
    /// </summary>
    public async Task<int> RevokeAllSessionsAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _sessionRepository.RevokeAllByUserIdAsync(userId, cancellationToken);
    }

    /// <summary>
    /// Updates the last activity timestamp for a session.
    /// </summary>
    public async Task UpdateSessionActivityAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        await _sessionRepository.UpdateLastActivityAsync(sessionId, cancellationToken);
    }

    /// <summary>
    /// Enforces concurrent session limits for a user.
    /// Revokes oldest sessions if limit is exceeded.
    /// </summary>
    public async Task EnforceConcurrentSessionLimitAsync(
        string userId,
        int maxConcurrentSessions,
        CancellationToken cancellationToken = default)
    {
        var activeSessions = await GetActiveSessionsAsync(userId, cancellationToken);

        if (activeSessions.Count > maxConcurrentSessions)
        {
            // Sort by last activity (oldest first)
            var sessionsToRevoke = activeSessions
                .OrderBy(s => s.LastActivityAt)
                .Take(activeSessions.Count - maxConcurrentSessions)
                .ToList();

            foreach (var session in sessionsToRevoke)
            {
                await RevokeSessionAsync(session.Id, cancellationToken);
            }
        }
    }
}
