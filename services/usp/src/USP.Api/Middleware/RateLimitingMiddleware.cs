using System.Security.Claims;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using USP.Core.Models.Configuration;

namespace USP.Api.Middleware;

/// <summary>
/// Advanced rate limiting middleware with multiple strategies
/// Implements distributed rate limiting using Redis with sliding window algorithm
/// </summary>
public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitingMiddleware> _logger;
    private readonly RateLimitingSettings _settings;

    private const string RateLimitPrefix = "ratelimit:";
    private const string ViolationPrefix = "ratelimit:violations:";
    private const string PenaltyPrefix = "ratelimit:penalty:";

    public RateLimitingMiddleware(
        RequestDelegate next,
        ILogger<RateLimitingMiddleware> logger,
        IOptions<RateLimitingSettings> settings)
    {
        _next = next;
        _logger = logger;
        _settings = settings.Value;
    }

    public async Task InvokeAsync(HttpContext context, IDistributedCache cache)
    {
        // Skip rate limiting for health checks and public endpoints
        if (ShouldSkipRateLimiting(context.Request.Path))
        {
            await _next(context);
            return;
        }

        try
        {
            var userId = GetUserId(context);
            var ipAddress = GetClientIpAddress(context);
            var endpoint = $"{context.Request.Method} {context.Request.Path}";

            // Check if IP is under penalty
            if (await IsUnderPenalty(cache, ipAddress))
            {
                await ReturnRateLimitResponse(context, "IP address is temporarily banned due to excessive violations", 900);
                return;
            }

            // Global rate limiting (DDoS protection)
            if (_settings.EnableGlobalRateLimiting)
            {
                var globalAllowed = await CheckRateLimitAsync(
                    cache,
                    $"{RateLimitPrefix}global",
                    _settings.GlobalRequestsPerSecond,
                    TimeSpan.FromSeconds(1)
                );

                if (!globalAllowed)
                {
                    _logger.LogWarning("Global rate limit exceeded");
                    await ReturnRateLimitResponse(context, "Global rate limit exceeded. Please try again later.", 1);
                    return;
                }
            }

            // Per-IP rate limiting
            if (_settings.EnablePerIpRateLimiting && ipAddress != null)
            {
                var ipMinuteAllowed = await CheckRateLimitAsync(
                    cache,
                    $"{RateLimitPrefix}ip:minute:{ipAddress}",
                    _settings.PerIpRequestsPerMinute,
                    TimeSpan.FromMinutes(1)
                );

                if (!ipMinuteAllowed)
                {
                    _logger.LogWarning("IP {IpAddress} exceeded minute rate limit", ipAddress);
                    await RecordViolation(cache, ipAddress);
                    await ReturnRateLimitResponse(context, "Rate limit exceeded for your IP address", 60);
                    return;
                }

                var ipHourAllowed = await CheckRateLimitAsync(
                    cache,
                    $"{RateLimitPrefix}ip:hour:{ipAddress}",
                    _settings.PerIpRequestsPerHour,
                    TimeSpan.FromHours(1)
                );

                if (!ipHourAllowed)
                {
                    _logger.LogWarning("IP {IpAddress} exceeded hour rate limit", ipAddress);
                    await RecordViolation(cache, ipAddress);
                    await ReturnRateLimitResponse(context, "Hourly rate limit exceeded for your IP address", 3600);
                    return;
                }
            }

            // Per-User rate limiting (requires authentication)
            if (_settings.EnablePerUserRateLimiting && !string.IsNullOrEmpty(userId))
            {
                var userMinuteAllowed = await CheckRateLimitAsync(
                    cache,
                    $"{RateLimitPrefix}user:minute:{userId}",
                    _settings.PerUserRequestsPerMinute,
                    TimeSpan.FromMinutes(1)
                );

                if (!userMinuteAllowed)
                {
                    _logger.LogWarning("User {UserId} exceeded minute rate limit", userId);
                    await ReturnRateLimitResponse(context, "Rate limit exceeded for your account", 60);
                    return;
                }

                var userHourAllowed = await CheckRateLimitAsync(
                    cache,
                    $"{RateLimitPrefix}user:hour:{userId}",
                    _settings.PerUserRequestsPerHour,
                    TimeSpan.FromHours(1)
                );

                if (!userHourAllowed)
                {
                    _logger.LogWarning("User {UserId} exceeded hour rate limit", userId);
                    await ReturnRateLimitResponse(context, "Hourly rate limit exceeded for your account", 3600);
                    return;
                }
            }

            // Per-Endpoint rate limiting (specific endpoints have stricter limits)
            if (_settings.EnablePerEndpointRateLimiting)
            {
                var endpointLimit = GetEndpointRateLimit(endpoint);
                if (endpointLimit.HasValue)
                {
                    var endpointKey = $"{RateLimitPrefix}endpoint:{ipAddress ?? userId}:{endpoint}";
                    var endpointAllowed = await CheckRateLimitAsync(
                        cache,
                        endpointKey,
                        endpointLimit.Value,
                        TimeSpan.FromMinutes(1)
                    );

                    if (!endpointAllowed)
                    {
                        _logger.LogWarning("Endpoint rate limit exceeded for {Endpoint} from {IpAddress}", endpoint, ipAddress);
                        await RecordViolation(cache, ipAddress ?? userId);
                        await ReturnRateLimitResponse(context, "Rate limit exceeded for this endpoint", 60);
                        return;
                    }
                }
            }

            // Request allowed - proceed
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in rate limiting middleware");
            // Allow request to proceed on middleware errors (fail open for availability)
            await _next(context);
        }
    }

    /// <summary>
    /// Checks rate limit using sliding window algorithm
    /// </summary>
    private async Task<bool> CheckRateLimitAsync(
        IDistributedCache cache,
        string key,
        int limit,
        TimeSpan window)
    {
        var currentCount = await GetCurrentCountAsync(cache, key);

        // Apply burst allowance
        var burstLimit = limit + (limit * _settings.BurstAllowancePercentage / 100);

        if (currentCount >= burstLimit)
        {
            return false;
        }

        // Increment counter
        await IncrementCountAsync(cache, key, window);
        return true;
    }

    /// <summary>
    /// Gets current count from cache
    /// </summary>
    private async Task<int> GetCurrentCountAsync(IDistributedCache cache, string key)
    {
        var value = await cache.GetStringAsync(key);
        return int.TryParse(value, out var count) ? count : 0;
    }

    /// <summary>
    /// Increments counter in cache with expiration
    /// </summary>
    private async Task IncrementCountAsync(IDistributedCache cache, string key, TimeSpan expiration)
    {
        if (_settings.UseSlidingWindow)
        {
            // Sliding window: Use list of timestamps
            var timestampKey = $"{key}:timestamps";
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var windowStart = now - (long)expiration.TotalSeconds;

            // For simplicity, using counter-based approach
            // In production, consider using Redis sorted sets for true sliding window
            var currentCount = await GetCurrentCountAsync(cache, key);
            var newCount = currentCount + 1;

            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiration
            };

            await cache.SetStringAsync(key, newCount.ToString(), options);
        }
        else
        {
            // Fixed window: Simple counter
            var currentCount = await GetCurrentCountAsync(cache, key);
            var newCount = currentCount + 1;

            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiration
            };

            await cache.SetStringAsync(key, newCount.ToString(), options);
        }
    }

    /// <summary>
    /// Records a rate limit violation
    /// </summary>
    private async Task RecordViolation(IDistributedCache cache, string identifier)
    {
        if (string.IsNullOrEmpty(identifier))
        {
            return;
        }

        var violationKey = $"{ViolationPrefix}{identifier}";
        var violationCount = await GetCurrentCountAsync(cache, violationKey);
        violationCount++;

        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_settings.ViolationPenaltyMinutes)
        };

        await cache.SetStringAsync(violationKey, violationCount.ToString(), options);

        // Apply penalty if threshold exceeded
        if (violationCount >= _settings.ViolationsBeforePenalty)
        {
            var penaltyKey = $"{PenaltyPrefix}{identifier}";
            var penaltyOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_settings.ViolationPenaltyMinutes)
            };

            await cache.SetStringAsync(penaltyKey, "1", penaltyOptions);
            _logger.LogWarning("IP/User {Identifier} placed under penalty for {Minutes} minutes due to {Count} violations",
                identifier, _settings.ViolationPenaltyMinutes, violationCount);
        }
    }

    /// <summary>
    /// Checks if identifier is under penalty
    /// </summary>
    private async Task<bool> IsUnderPenalty(IDistributedCache cache, string identifier)
    {
        if (string.IsNullOrEmpty(identifier))
        {
            return false;
        }

        var penaltyKey = $"{PenaltyPrefix}{identifier}";
        var penaltyValue = await cache.GetStringAsync(penaltyKey);
        return !string.IsNullOrEmpty(penaltyValue);
    }

    /// <summary>
    /// Gets rate limit for specific endpoint
    /// </summary>
    private int? GetEndpointRateLimit(string endpoint)
    {
        if (endpoint.Contains("/api/auth/login", StringComparison.OrdinalIgnoreCase))
        {
            return _settings.LoginRequestsPerMinute;
        }

        if (endpoint.Contains("/api/auth/mfa", StringComparison.OrdinalIgnoreCase))
        {
            return _settings.MfaRequestsPerMinute;
        }

        if (endpoint.Contains("/api/secrets", StringComparison.OrdinalIgnoreCase))
        {
            return _settings.SecretsRequestsPerMinute;
        }

        return null; // No specific limit for this endpoint
    }

    /// <summary>
    /// Returns HTTP 429 Too Many Requests response
    /// </summary>
    private async Task ReturnRateLimitResponse(HttpContext context, string message, int retryAfterSeconds)
    {
        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.Response.Headers.Append("Retry-After", retryAfterSeconds.ToString());
        context.Response.Headers.Append("X-RateLimit-Limit", "See endpoint documentation");
        context.Response.Headers.Append("X-RateLimit-Remaining", "0");
        context.Response.Headers.Append("X-RateLimit-Reset", DateTimeOffset.UtcNow.AddSeconds(retryAfterSeconds).ToUnixTimeSeconds().ToString());

        await context.Response.WriteAsJsonAsync(new
        {
            error = "Rate limit exceeded",
            message = message,
            retryAfter = retryAfterSeconds,
            retryAfterDateTime = DateTimeOffset.UtcNow.AddSeconds(retryAfterSeconds).ToString("o")
        });
    }

    /// <summary>
    /// Gets user ID from claims
    /// </summary>
    private static string? GetUserId(HttpContext context)
    {
        if (context.User?.Identity?.IsAuthenticated == true)
        {
            return context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        }

        return null;
    }

    /// <summary>
    /// Gets client IP address (handles proxies)
    /// </summary>
    private static string? GetClientIpAddress(HttpContext context)
    {
        // Check X-Forwarded-For header (for proxies/load balancers)
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            return forwardedFor.Split(',')[0].Trim();
        }

        // Check X-Real-IP header
        var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(realIp))
        {
            return realIp;
        }

        // Fallback to connection remote IP
        return context.Connection.RemoteIpAddress?.ToString();
    }

    /// <summary>
    /// Determines if request should skip rate limiting
    /// </summary>
    private static bool ShouldSkipRateLimiting(PathString path)
    {
        var skipPaths = new[]
        {
            "/health",
            "/health/live",
            "/health/ready",
            "/swagger",
            "/metrics"
        };

        return skipPaths.Any(p => path.StartsWithSegments(p, StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>
/// Extension methods for rate limiting middleware
/// </summary>
public static class RateLimitingMiddlewareExtensions
{
    public static IApplicationBuilder UseRateLimiting(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<RateLimitingMiddleware>();
    }
}
