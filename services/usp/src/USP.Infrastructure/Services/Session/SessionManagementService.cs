using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using USP.Core.Models.DTOs.Session;
using USP.Core.Models.Entities;
using USP.Core.Services.Session;
using USP.Infrastructure.Data;

namespace USP.Infrastructure.Services.Session;

/// <summary>
/// Session management service implementation
/// </summary>
public class SessionManagementService : ISessionManagementService
{
    private readonly ApplicationDbContext _context;
    private readonly IDistributedCache _cache;
    private readonly ILogger<SessionManagementService> _logger;
    private readonly IConfiguration _configuration;

    private const string SessionCacheKeyPrefix = "session:";
    private const int DefaultIdleTimeoutMinutes = 15;
    private const int DefaultAbsoluteTimeoutHours = 24;
    private const int DefaultMaxConcurrentSessions = 5;

    public SessionManagementService(
        ApplicationDbContext context,
        IDistributedCache cache,
        ILogger<SessionManagementService> logger,
        IConfiguration configuration)
    {
        _context = context;
        _cache = cache;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<List<SessionDto>> GetActiveSessionsAsync(Guid userId)
    {
        try
        {
            var sessions = await _context.Sessions
                .AsNoTracking()
                .Where(s => s.UserId == userId && !s.Revoked && s.ExpiresAt > DateTime.UtcNow)
                .OrderByDescending(s => s.LastActivity)
                .ToListAsync();

            return sessions.Select(s => MapToDto(s)).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active sessions for user {UserId}", userId);
            throw;
        }
    }

    public async Task<SessionDto?> GetSessionDetailsAsync(Guid sessionId, Guid userId)
    {
        try
        {
            var session = await _context.Sessions
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId);

            return session != null ? MapToDto(session) : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving session {SessionId}", sessionId);
            throw;
        }
    }

    public async Task<Guid> CreateSessionAsync(
        Guid userId,
        string tokenHash,
        string? refreshTokenHash,
        string ipAddress,
        string? userAgent,
        DateTime expiresAt)
    {
        try
        {
            var maxConcurrentSessions = _configuration.GetValue<int>("Session:MaxConcurrentSessions", DefaultMaxConcurrentSessions);
            var activeSessionCount = await GetActiveSessionCountAsync(userId);

            if (activeSessionCount >= maxConcurrentSessions)
            {
                _logger.LogWarning("User {UserId} has reached maximum concurrent sessions ({Count})", userId, maxConcurrentSessions);
                await RevokeOldestSessionAsync(userId);
            }

            IPAddress? parsedIpAddress = null;
            if (!string.IsNullOrEmpty(ipAddress))
            {
                IPAddress.TryParse(ipAddress, out parsedIpAddress);
            }

            var session = new Core.Models.Entities.Session
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                TokenHash = tokenHash,
                RefreshTokenHash = refreshTokenHash,
                IpAddress = parsedIpAddress,
                UserAgent = userAgent,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = expiresAt,
                LastActivity = DateTime.UtcNow,
                Revoked = false
            };

            _context.Sessions.Add(session);
            await _context.SaveChangesAsync();

            await CacheSessionAsync(session);

            _logger.LogInformation("Session created for user {UserId}, session ID {SessionId}", userId, session.Id);

            return session.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating session for user {UserId}", userId);
            throw;
        }
    }

    public async Task<bool> UpdateSessionActivityAsync(Guid sessionId)
    {
        try
        {
            var session = await _context.Sessions.FindAsync(sessionId);
            if (session == null || session.Revoked)
            {
                return false;
            }

            session.LastActivity = DateTime.UtcNow;

            var idleTimeoutMinutes = _configuration.GetValue<int>("Session:IdleTimeoutMinutes", DefaultIdleTimeoutMinutes);
            session.ExpiresAt = DateTime.UtcNow.AddMinutes(idleTimeoutMinutes);

            await _context.SaveChangesAsync();
            await CacheSessionAsync(session);

            _logger.LogDebug("Session {SessionId} activity updated", sessionId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating session activity for {SessionId}", sessionId);
            return false;
        }
    }

    public async Task<DateTime?> ExtendSessionAsync(Guid sessionId, Guid userId, int additionalMinutes = 15)
    {
        try
        {
            var session = await _context.Sessions
                .FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId && !s.Revoked);

            if (session == null)
            {
                _logger.LogWarning("Attempted to extend non-existent or unauthorized session {SessionId} for user {UserId}", sessionId, userId);
                return null;
            }

            if (session.ExpiresAt < DateTime.UtcNow)
            {
                _logger.LogWarning("Attempted to extend expired session {SessionId}", sessionId);
                return null;
            }

            var absoluteTimeoutHours = _configuration.GetValue<int>("Session:AbsoluteTimeoutHours", DefaultAbsoluteTimeoutHours);
            var maxExpirationTime = session.CreatedAt.AddHours(absoluteTimeoutHours);
            var newExpirationTime = DateTime.UtcNow.AddMinutes(additionalMinutes);

            if (newExpirationTime > maxExpirationTime)
            {
                newExpirationTime = maxExpirationTime;
                _logger.LogInformation("Session {SessionId} extension limited by absolute timeout", sessionId);
            }

            session.ExpiresAt = newExpirationTime;
            session.LastActivity = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            await CacheSessionAsync(session);

            _logger.LogInformation("Session {SessionId} extended to {ExpiresAt}", sessionId, newExpirationTime);

            return newExpirationTime;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extending session {SessionId}", sessionId);
            return null;
        }
    }

    public async Task<bool> RevokeSessionAsync(Guid sessionId, Guid userId)
    {
        try
        {
            var session = await _context.Sessions
                .FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId);

            if (session == null)
            {
                _logger.LogWarning("Attempted to revoke non-existent or unauthorized session {SessionId} for user {UserId}", sessionId, userId);
                return false;
            }

            session.Revoked = true;
            session.RevokedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            await RemoveSessionFromCacheAsync(sessionId);

            _logger.LogInformation("Session {SessionId} revoked for user {UserId}", sessionId, userId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking session {SessionId}", sessionId);
            return false;
        }
    }

    public async Task<int> RevokeOtherSessionsAsync(Guid userId, Guid currentSessionId)
    {
        try
        {
            var sessions = await _context.Sessions
                .Where(s => s.UserId == userId && s.Id != currentSessionId && !s.Revoked)
                .ToListAsync();

            var revokedCount = 0;
            foreach (var session in sessions)
            {
                session.Revoked = true;
                session.RevokedAt = DateTime.UtcNow;
                await RemoveSessionFromCacheAsync(session.Id);
                revokedCount++;
            }

            if (revokedCount > 0)
            {
                await _context.SaveChangesAsync();
            }

            _logger.LogInformation("Revoked {Count} other sessions for user {UserId}", revokedCount, userId);

            return revokedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking other sessions for user {UserId}", userId);
            throw;
        }
    }

    public async Task<int> RevokeAllSessionsAsync(Guid userId)
    {
        try
        {
            var sessions = await _context.Sessions
                .Where(s => s.UserId == userId && !s.Revoked)
                .ToListAsync();

            var revokedCount = 0;
            foreach (var session in sessions)
            {
                session.Revoked = true;
                session.RevokedAt = DateTime.UtcNow;
                await RemoveSessionFromCacheAsync(session.Id);
                revokedCount++;
            }

            if (revokedCount > 0)
            {
                await _context.SaveChangesAsync();
            }

            _logger.LogInformation("Revoked all {Count} sessions for user {UserId}", revokedCount, userId);

            return revokedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking all sessions for user {UserId}", userId);
            throw;
        }
    }

    public async Task<int> CleanupExpiredSessionsAsync()
    {
        try
        {
            var expiredSessions = await _context.Sessions
                .Where(s => !s.Revoked && s.ExpiresAt < DateTime.UtcNow)
                .ToListAsync();

            var cleanupCount = 0;
            foreach (var session in expiredSessions)
            {
                session.Revoked = true;
                session.RevokedAt = DateTime.UtcNow;
                await RemoveSessionFromCacheAsync(session.Id);
                cleanupCount++;
            }

            if (cleanupCount > 0)
            {
                await _context.SaveChangesAsync();
            }

            _logger.LogInformation("Cleaned up {Count} expired sessions", cleanupCount);

            return cleanupCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up expired sessions");
            throw;
        }
    }

    public async Task<int> GetActiveSessionCountAsync(Guid userId)
    {
        try
        {
            return await _context.Sessions
                .CountAsync(s => s.UserId == userId && !s.Revoked && s.ExpiresAt > DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active session count for user {UserId}", userId);
            throw;
        }
    }

    public async Task<bool> ValidateSessionAsync(Guid sessionId, Guid userId)
    {
        try
        {
            var session = await _context.Sessions
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId);

            if (session == null)
            {
                return false;
            }

            return !session.Revoked && session.ExpiresAt > DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating session {SessionId}", sessionId);
            return false;
        }
    }

    #region Private Helper Methods

    private async Task RevokeOldestSessionAsync(Guid userId)
    {
        var oldestSession = await _context.Sessions
            .Where(s => s.UserId == userId && !s.Revoked)
            .OrderBy(s => s.LastActivity)
            .FirstOrDefaultAsync();

        if (oldestSession != null)
        {
            oldestSession.Revoked = true;
            oldestSession.RevokedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            await RemoveSessionFromCacheAsync(oldestSession.Id);

            _logger.LogInformation("Revoked oldest session {SessionId} for user {UserId} due to concurrent session limit", oldestSession.Id, userId);
        }
    }

    private async Task CacheSessionAsync(Core.Models.Entities.Session session)
    {
        try
        {
            var cacheKey = $"{SessionCacheKeyPrefix}{session.Id}";
            var cacheOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpiration = session.ExpiresAt
            };

            var sessionData = System.Text.Json.JsonSerializer.Serialize(new
            {
                session.Id,
                session.UserId,
                session.ExpiresAt,
                session.Revoked
            });

            await _cache.SetStringAsync(cacheKey, sessionData, cacheOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cache session {SessionId}, continuing without cache", session.Id);
        }
    }

    private async Task RemoveSessionFromCacheAsync(Guid sessionId)
    {
        try
        {
            var cacheKey = $"{SessionCacheKeyPrefix}{sessionId}";
            await _cache.RemoveAsync(cacheKey);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to remove session {SessionId} from cache", sessionId);
        }
    }

    private static SessionDto MapToDto(Core.Models.Entities.Session session)
    {
        return new SessionDto
        {
            Id = session.Id,
            UserId = session.UserId,
            IpAddress = session.IpAddress?.ToString() ?? string.Empty,
            UserAgent = session.UserAgent,
            CreatedAt = session.CreatedAt,
            ExpiresAt = session.ExpiresAt,
            LastActivity = session.LastActivity,
            IsCurrent = false
        };
    }

    #endregion
}
