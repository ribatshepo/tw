using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using USP.Core.Domain.Entities.Identity;
using USP.Core.Interfaces.Repositories;
using USP.Infrastructure.Persistence;

namespace USP.Infrastructure.Repositories;

/// <summary>
/// Redis-backed session repository with PostgreSQL fallback for audit.
/// </summary>
public class SessionRepository : ISessionRepository
{
    private readonly ApplicationDbContext _context;
    private readonly IDistributedCache _cache;
    private const string SessionKeyPrefix = "session:";
    private const string RefreshTokenKeyPrefix = "refresh_token:";
    private const string UserSessionsKeyPrefix = "user_sessions:";

    public SessionRepository(ApplicationDbContext context, IDistributedCache cache)
    {
        _context = context;
        _cache = cache;
    }

    /// <summary>
    /// Stores a session in Redis with TTL and PostgreSQL for audit.
    /// </summary>
    public async Task StoreAsync(
        Session session,
        int accessTokenTtl = 3600,
        int refreshTokenTtl = 604800,
        CancellationToken cancellationToken = default)
    {
        // Store in PostgreSQL for audit
        _context.Sessions.Add(session);
        await _context.SaveChangesAsync(cancellationToken);

        // Serialize session to JSON
        var sessionJson = JsonSerializer.Serialize(session);

        // Store in Redis with access token TTL
        var sessionKey = $"{SessionKeyPrefix}{session.Id}";
        var sessionOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(accessTokenTtl)
        };
        await _cache.SetStringAsync(sessionKey, sessionJson, sessionOptions, cancellationToken);

        // Store refresh token mapping with longer TTL
        var refreshTokenKey = $"{RefreshTokenKeyPrefix}{session.RefreshToken}";
        var refreshTokenOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(refreshTokenTtl)
        };
        await _cache.SetStringAsync(refreshTokenKey, session.Id, refreshTokenOptions, cancellationToken);

        // Add to user's active sessions set
        await AddToUserSessionsSetAsync(session.UserId, session.Id, cancellationToken);
    }

    /// <summary>
    /// Gets a session by ID from Redis (fast path) or PostgreSQL (fallback).
    /// </summary>
    public async Task<Session?> GetByIdAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        // Try Redis first (fast path)
        var sessionKey = $"{SessionKeyPrefix}{sessionId}";
        var sessionJson = await _cache.GetStringAsync(sessionKey, cancellationToken);

        if (!string.IsNullOrEmpty(sessionJson))
        {
            return JsonSerializer.Deserialize<Session>(sessionJson);
        }

        // Fallback to PostgreSQL (for audit queries)
        return await _context.Sessions
            .FirstOrDefaultAsync(s => s.Id == sessionId && !s.IsRevoked, cancellationToken);
    }

    /// <summary>
    /// Gets a session by refresh token from Redis.
    /// </summary>
    public async Task<Session?> GetByRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        // Get session ID from refresh token mapping
        var refreshTokenKey = $"{RefreshTokenKeyPrefix}{refreshToken}";
        var sessionId = await _cache.GetStringAsync(refreshTokenKey, cancellationToken);

        if (string.IsNullOrEmpty(sessionId))
        {
            return null;
        }

        // Get session by ID
        return await GetByIdAsync(sessionId, cancellationToken);
    }

    /// <summary>
    /// Gets all active sessions for a user from Redis and PostgreSQL.
    /// </summary>
    public async Task<List<Session>> GetActiveSessionsByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        // Get session IDs from user's active sessions set in Redis
        var userSessionsKey = $"{UserSessionsKeyPrefix}{userId}";
        var sessionIdsJson = await _cache.GetStringAsync(userSessionsKey, cancellationToken);

        var sessions = new List<Session>();

        if (!string.IsNullOrEmpty(sessionIdsJson))
        {
            var sessionIds = JsonSerializer.Deserialize<List<string>>(sessionIdsJson) ?? new List<string>();

            foreach (var sessionId in sessionIds)
            {
                var session = await GetByIdAsync(sessionId, cancellationToken);
                if (session != null)
                {
                    sessions.Add(session);
                }
            }
        }

        // Also check PostgreSQL for any sessions not in Redis
        var dbSessions = await _context.Sessions
            .Where(s => s.UserId == userId && !s.IsRevoked && s.ExpiresAt > DateTime.UtcNow)
            .ToListAsync(cancellationToken);

        // Merge and deduplicate
        var sessionDict = sessions.ToDictionary(s => s.Id);
        foreach (var dbSession in dbSessions)
        {
            if (!sessionDict.ContainsKey(dbSession.Id))
            {
                sessions.Add(dbSession);
            }
        }

        return sessions;
    }

    /// <summary>
    /// Revokes a session (deletes from Redis and marks as revoked in PostgreSQL).
    /// </summary>
    public async Task<bool> RevokeAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        // Get session from PostgreSQL
        var session = await _context.Sessions.FindAsync(new object[] { sessionId }, cancellationToken);
        if (session == null)
        {
            return false;
        }

        // Mark as revoked in PostgreSQL
        session.RevokedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        // Delete from Redis
        var sessionKey = $"{SessionKeyPrefix}{sessionId}";
        await _cache.RemoveAsync(sessionKey, cancellationToken);

        // Delete refresh token mapping
        var refreshTokenKey = $"{RefreshTokenKeyPrefix}{session.RefreshToken}";
        await _cache.RemoveAsync(refreshTokenKey, cancellationToken);

        // Remove from user's active sessions set
        await RemoveFromUserSessionsSetAsync(session.UserId, sessionId, cancellationToken);

        return true;
    }

    /// <summary>
    /// Revokes all sessions for a user.
    /// </summary>
    public async Task<int> RevokeAllByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        // Get all active sessions
        var sessions = await GetActiveSessionsByUserIdAsync(userId, cancellationToken);

        var count = 0;
        foreach (var session in sessions)
        {
            var revoked = await RevokeAsync(session.Id, cancellationToken);
            if (revoked)
            {
                count++;
            }
        }

        // Clear user's active sessions set
        var userSessionsKey = $"{UserSessionsKeyPrefix}{userId}";
        await _cache.RemoveAsync(userSessionsKey, cancellationToken);

        return count;
    }

    /// <summary>
    /// Updates the last activity timestamp for a session in Redis.
    /// </summary>
    public async Task UpdateLastActivityAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        var session = await GetByIdAsync(sessionId, cancellationToken);
        if (session != null)
        {
            session.LastActivityAt = DateTime.UtcNow;

            // Update in Redis
            var sessionKey = $"{SessionKeyPrefix}{sessionId}";
            var sessionJson = JsonSerializer.Serialize(session);
            await _cache.SetStringAsync(sessionKey, sessionJson, cancellationToken);

            // Update in PostgreSQL
            var dbSession = await _context.Sessions.FindAsync(new object[] { sessionId }, cancellationToken);
            if (dbSession != null)
            {
                dbSession.LastActivityAt = DateTime.UtcNow;
                await _context.SaveChangesAsync(cancellationToken);
            }
        }
    }

    /// <summary>
    /// Counts active sessions for a user.
    /// </summary>
    public async Task<int> CountActiveSessionsByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        var sessions = await GetActiveSessionsByUserIdAsync(userId, cancellationToken);
        return sessions.Count;
    }

    private async Task AddToUserSessionsSetAsync(string userId, string sessionId, CancellationToken cancellationToken)
    {
        var userSessionsKey = $"{UserSessionsKeyPrefix}{userId}";
        var sessionIdsJson = await _cache.GetStringAsync(userSessionsKey, cancellationToken);

        var sessionIds = string.IsNullOrEmpty(sessionIdsJson)
            ? new List<string>()
            : JsonSerializer.Deserialize<List<string>>(sessionIdsJson) ?? new List<string>();

        if (!sessionIds.Contains(sessionId))
        {
            sessionIds.Add(sessionId);
        }

        var updatedJson = JsonSerializer.Serialize(sessionIds);
        await _cache.SetStringAsync(userSessionsKey, updatedJson, cancellationToken);
    }

    private async Task RemoveFromUserSessionsSetAsync(string userId, string sessionId, CancellationToken cancellationToken)
    {
        var userSessionsKey = $"{UserSessionsKeyPrefix}{userId}";
        var sessionIdsJson = await _cache.GetStringAsync(userSessionsKey, cancellationToken);

        if (!string.IsNullOrEmpty(sessionIdsJson))
        {
            var sessionIds = JsonSerializer.Deserialize<List<string>>(sessionIdsJson) ?? new List<string>();
            sessionIds.Remove(sessionId);

            var updatedJson = JsonSerializer.Serialize(sessionIds);
            await _cache.SetStringAsync(userSessionsKey, updatedJson, cancellationToken);
        }
    }
}
