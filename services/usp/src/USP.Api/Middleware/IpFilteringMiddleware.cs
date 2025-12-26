using System.Net;
using MaxMind.GeoIP2;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using USP.Core.Models.Configuration;

namespace USP.Api.Middleware;

/// <summary>
/// IP filtering middleware with whitelist/blacklist and temporary banning
/// Supports CIDR notation and geo-blocking
/// </summary>
public class IpFilteringMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<IpFilteringMiddleware> _logger;
    private readonly IpFilteringSettings _settings;

    private const string BannedIpPrefix = "ipfilter:banned:";
    private const string FailedAttemptsPrefix = "ipfilter:failed:";

    public IpFilteringMiddleware(
        RequestDelegate next,
        ILogger<IpFilteringMiddleware> logger,
        IOptions<IpFilteringSettings> settings)
    {
        _next = next;
        _logger = logger;
        _settings = settings.Value;
    }

    public async Task InvokeAsync(HttpContext context, IDistributedCache cache)
    {
        // Skip IP filtering for health checks
        if (ShouldSkipFiltering(context.Request.Path))
        {
            await _next(context);
            return;
        }

        try
        {
            var ipAddress = GetClientIpAddress(context);

            if (string.IsNullOrEmpty(ipAddress))
            {
                _logger.LogWarning("Unable to determine client IP address");
                await _next(context);
                return;
            }

            // Check if IP is temporarily banned
            if (await IsIpBanned(cache, ipAddress))
            {
                _logger.LogWarning("Access denied for temporarily banned IP: {IpAddress}", ipAddress);
                await ReturnForbiddenResponse(context, "IP address is temporarily banned");
                return;
            }

            // Check blacklist (if enabled)
            if (_settings.EnableBlacklist)
            {
                if (IsIpInList(ipAddress, _settings.BlacklistIps))
                {
                    _logger.LogWarning("Access denied for blacklisted IP: {IpAddress}", ipAddress);
                    await ReturnForbiddenResponse(context, "Access denied");
                    return;
                }
            }

            // Check whitelist (if enabled)
            if (_settings.EnableWhitelist)
            {
                if (!IsIpInList(ipAddress, _settings.WhitelistIps))
                {
                    _logger.LogWarning("Access denied for non-whitelisted IP: {IpAddress}", ipAddress);
                    await ReturnForbiddenResponse(context, "Access denied");
                    return;
                }
            }

            // Geo-blocking (if enabled)
            if (_settings.EnableGeoBlocking && _settings.BlockedCountries.Any())
            {
                var countryCode = await GetCountryCodeAsync(ipAddress);
                if (!string.IsNullOrEmpty(countryCode) &&
                    _settings.BlockedCountries.Contains(countryCode, StringComparer.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("Access denied for IP from blocked country {CountryCode}: {IpAddress}",
                        countryCode, ipAddress);
                    await ReturnForbiddenResponse(context, "Access denied from your location");
                    return;
                }
            }

            // IP allowed - proceed with request
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in IP filtering middleware");
            // Allow request to proceed on errors (fail open)
            await _next(context);
        }
    }

    /// <summary>
    /// Records failed authentication attempt and bans IP if threshold exceeded
    /// </summary>
    public async Task RecordFailedAttemptAsync(IDistributedCache cache, string ipAddress)
    {
        if (string.IsNullOrEmpty(ipAddress))
        {
            return;
        }

        var key = $"{FailedAttemptsPrefix}{ipAddress}";
        var attempts = await GetFailedAttemptsAsync(cache, ipAddress);
        attempts++;

        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_settings.FailedAttemptsWindowMinutes)
        };

        await cache.SetStringAsync(key, attempts.ToString(), options);

        _logger.LogInformation("Failed authentication attempt {Attempts} for IP {IpAddress}", attempts, ipAddress);

        // Ban IP if threshold exceeded
        if (attempts >= _settings.FailedAttemptsBeforeBan)
        {
            await BanIpTemporarilyAsync(cache, ipAddress);
        }
    }

    /// <summary>
    /// Gets failed attempts count for IP
    /// </summary>
    private async Task<int> GetFailedAttemptsAsync(IDistributedCache cache, string ipAddress)
    {
        var key = $"{FailedAttemptsPrefix}{ipAddress}";
        var value = await cache.GetStringAsync(key);
        return int.TryParse(value, out var count) ? count : 0;
    }

    /// <summary>
    /// Temporarily bans an IP address
    /// </summary>
    private async Task BanIpTemporarilyAsync(IDistributedCache cache, string ipAddress)
    {
        var key = $"{BannedIpPrefix}{ipAddress}";
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_settings.TemporaryBanDurationMinutes)
        };

        await cache.SetStringAsync(key, "1", options);

        _logger.LogWarning("IP {IpAddress} temporarily banned for {Minutes} minutes due to excessive failed attempts",
            ipAddress, _settings.TemporaryBanDurationMinutes);
    }

    /// <summary>
    /// Checks if IP is currently banned
    /// </summary>
    private async Task<bool> IsIpBanned(IDistributedCache cache, string ipAddress)
    {
        var key = $"{BannedIpPrefix}{ipAddress}";
        var value = await cache.GetStringAsync(key);
        return !string.IsNullOrEmpty(value);
    }

    /// <summary>
    /// Checks if IP address is in the specified list (supports CIDR notation)
    /// </summary>
    private bool IsIpInList(string ipAddress, List<string> ipList)
    {
        if (!IPAddress.TryParse(ipAddress, out var ip))
        {
            return false;
        }

        foreach (var entry in ipList)
        {
            // Check for CIDR notation
            if (entry.Contains('/'))
            {
                if (IsIpInCidrRange(ip, entry))
                {
                    return true;
                }
            }
            else
            {
                // Exact match
                if (entry.Equals(ipAddress, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Checks if IP is within CIDR range
    /// </summary>
    private bool IsIpInCidrRange(IPAddress ipAddress, string cidrNotation)
    {
        try
        {
            var parts = cidrNotation.Split('/');
            if (parts.Length != 2)
            {
                return false;
            }

            if (!IPAddress.TryParse(parts[0], out var networkAddress))
            {
                return false;
            }

            if (!int.TryParse(parts[1], out var prefixLength))
            {
                return false;
            }

            // Convert IPs to bytes
            var ipBytes = ipAddress.GetAddressBytes();
            var networkBytes = networkAddress.GetAddressBytes();

            // Ensure same address family
            if (ipBytes.Length != networkBytes.Length)
            {
                return false;
            }

            // Calculate mask
            var maskBits = prefixLength;
            for (int i = 0; i < ipBytes.Length; i++)
            {
                var mask = maskBits >= 8 ? 0xFF : (byte)(0xFF << (8 - maskBits));
                maskBits = Math.Max(0, maskBits - 8);

                if ((ipBytes[i] & mask) != (networkBytes[i] & mask))
                {
                    return false;
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking CIDR range for {IpAddress} in {CidrNotation}", ipAddress, cidrNotation);
            return false;
        }
    }

    /// <summary>
    /// Gets country code for IP address using GeoIP2 (if configured)
    /// </summary>
    private async Task<string?> GetCountryCodeAsync(string ipAddress)
    {
        try
        {
            if (!_settings.EnableGeoBlocking || string.IsNullOrEmpty(_settings.GeoIp2DatabasePath))
            {
                _logger.LogDebug("Geo-blocking disabled or database path not configured");
                return null;
            }

            // Check if database file exists
            if (!File.Exists(_settings.GeoIp2DatabasePath))
            {
                _logger.LogError("GeoIP2 database not found at {Path}. Geo-blocking will not function.", _settings.GeoIp2DatabasePath);
                return null;
            }

            // Parse IP address
            if (!IPAddress.TryParse(ipAddress, out var ip))
            {
                _logger.LogWarning("Invalid IP address format for geo-lookup: {IpAddress}", ipAddress);
                return null;
            }

            // Use MaxMind GeoIP2 to lookup country
            await Task.CompletedTask; // Keep async signature
            using var reader = new DatabaseReader(_settings.GeoIp2DatabasePath);
            var response = reader.Country(ip);

            _logger.LogDebug("IP {IpAddress} resolved to country {CountryCode}", ipAddress, response.Country.IsoCode);
            return response.Country.IsoCode;
        }
        catch (MaxMind.GeoIP2.Exceptions.AddressNotFoundException)
        {
            _logger.LogWarning("IP address {IpAddress} not found in GeoIP2 database (likely private/internal IP)", ipAddress);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error looking up country for IP {IpAddress}", ipAddress);
            return null;
        }
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
    /// Determines if request should skip IP filtering
    /// </summary>
    private static bool ShouldSkipFiltering(PathString path)
    {
        var skipPaths = new[]
        {
            "/health",
            "/health/live",
            "/health/ready"
        };

        return skipPaths.Any(p => path.StartsWithSegments(p, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Returns HTTP 403 Forbidden response
    /// </summary>
    private async Task ReturnForbiddenResponse(HttpContext context, string message)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(new
        {
            error = "Forbidden",
            message = message
        });
    }
}

/// <summary>
/// Extension methods for IP filtering middleware
/// </summary>
public static class IpFilteringMiddlewareExtensions
{
    public static IApplicationBuilder UseIpFiltering(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<IpFilteringMiddleware>();
    }
}
