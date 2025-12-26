using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using USP.Core.Models.Configuration;

namespace USP.Api.Middleware;

/// <summary>
/// API threat protection middleware
/// Detects SQL injection, XSS, path traversal, and other common attacks
/// </summary>
public class ApiThreatProtectionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiThreatProtectionMiddleware> _logger;
    private readonly ApiThreatProtectionSettings _settings;

    private const string RapidRequestPrefix = "threat:rapid:";

    public ApiThreatProtectionMiddleware(
        RequestDelegate next,
        ILogger<ApiThreatProtectionMiddleware> logger,
        IOptions<ApiThreatProtectionSettings> settings)
    {
        _next = next;
        _logger = logger;
        _settings = settings.Value;
    }

    public async Task InvokeAsync(HttpContext context, IDistributedCache cache)
    {
        try
        {
            var ipAddress = GetClientIpAddress(context);

            // Check for rapid sequential requests (suspicious pattern)
            if (_settings.EnableSuspiciousPatternDetection && !string.IsNullOrEmpty(ipAddress))
            {
                var isRapid = await CheckRapidRequestsAsync(cache, ipAddress);
                if (isRapid)
                {
                    _logger.LogWarning("Suspicious rapid requests detected from {IpAddress}", ipAddress);
                    await LogThreatAsync("Rapid sequential requests", ipAddress, context.Request.Path);

                    if (_settings.BlockOnThreatDetection)
                    {
                        await ReturnThreatBlockedResponse(context, "Suspicious activity detected");
                        return;
                    }
                }
            }

            // Validate headers
            if (_settings.EnableHeaderValidation)
            {
                var headerThreat = ValidateHeaders(context.Request);
                if (headerThreat != null)
                {
                    _logger.LogWarning("Header validation failed: {Threat}", headerThreat);
                    await LogThreatAsync(headerThreat, ipAddress, context.Request.Path);

                    if (_settings.BlockOnThreatDetection)
                    {
                        await ReturnThreatBlockedResponse(context, "Invalid request headers");
                        return;
                    }
                }
            }

            // Check query string for threats
            if (context.Request.QueryString.HasValue)
            {
                var queryThreat = DetectThreatsInString(context.Request.QueryString.Value);
                if (queryThreat != null)
                {
                    _logger.LogWarning("Threat detected in query string: {Threat}", queryThreat);
                    await LogThreatAsync(queryThreat, ipAddress, context.Request.Path);

                    if (_settings.BlockOnThreatDetection)
                    {
                        await ReturnThreatBlockedResponse(context, "Malicious request detected");
                        return;
                    }
                }
            }

            // Check URL path for path traversal
            if (_settings.EnablePathTraversalDetection)
            {
                var pathThreat = DetectPathTraversal(context.Request.Path.Value ?? string.Empty);
                if (pathThreat)
                {
                    _logger.LogWarning("Path traversal detected in {Path}", context.Request.Path);
                    await LogThreatAsync("Path traversal attempt", ipAddress, context.Request.Path);

                    if (_settings.BlockOnThreatDetection)
                    {
                        await ReturnThreatBlockedResponse(context, "Invalid request path");
                        return;
                    }
                }
            }

            // Check request body (for POST/PUT/PATCH)
            if (context.Request.Method != HttpMethod.Get.Method &&
                context.Request.Method != HttpMethod.Head.Method &&
                context.Request.ContentLength > 0)
            {
                // Validate body size
                if (context.Request.ContentLength > _settings.MaxRequestBodySize)
                {
                    _logger.LogWarning("Request body size {Size} exceeds limit {Limit}",
                        context.Request.ContentLength, _settings.MaxRequestBodySize);
                    await LogThreatAsync("Oversized request body", ipAddress, context.Request.Path);

                    if (_settings.BlockOnThreatDetection)
                    {
                        await ReturnThreatBlockedResponse(context, "Request body too large");
                        return;
                    }
                }

                // Read and scan body
                context.Request.EnableBuffering();
                var body = await ReadRequestBodyAsync(context.Request);

                // Detect threats in body
                var bodyThreat = DetectThreatsInString(body);
                if (bodyThreat != null)
                {
                    _logger.LogWarning("Threat detected in request body: {Threat}", bodyThreat);
                    await LogThreatAsync(bodyThreat, ipAddress, context.Request.Path);

                    if (_settings.BlockOnThreatDetection)
                    {
                        await ReturnThreatBlockedResponse(context, "Malicious request content detected");
                        return;
                    }
                }

                // Validate JSON depth (if JSON content)
                if (_settings.EnableJsonDepthLimiting &&
                    context.Request.ContentType?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true)
                {
                    var jsonDepthExceeded = CheckJsonDepth(body);
                    if (jsonDepthExceeded)
                    {
                        _logger.LogWarning("JSON depth exceeds limit in request");
                        await LogThreatAsync("Excessive JSON depth", ipAddress, context.Request.Path);

                        if (_settings.BlockOnThreatDetection)
                        {
                            await ReturnThreatBlockedResponse(context, "Invalid JSON structure");
                            return;
                        }
                    }
                }

                // Reset body stream
                context.Request.Body.Position = 0;
            }

            // No threats detected - proceed
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in threat protection middleware");
            // Allow request to proceed on errors (fail open)
            await _next(context);
        }
    }

    /// <summary>
    /// Detects various threats in a string
    /// </summary>
    private string? DetectThreatsInString(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return null;
        }

        // SQL injection detection
        if (_settings.EnableSqlInjectionDetection)
        {
            foreach (var pattern in _settings.SqlInjectionPatterns)
            {
                if (Regex.IsMatch(input, pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline, TimeSpan.FromMilliseconds(100)))
                {
                    return $"SQL injection attempt (pattern: {pattern})";
                }
            }
        }

        // XSS detection
        if (_settings.EnableXssDetection)
        {
            foreach (var pattern in _settings.XssPatterns)
            {
                if (Regex.IsMatch(input, pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline, TimeSpan.FromMilliseconds(100)))
                {
                    return $"XSS attempt (pattern: {pattern})";
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Detects path traversal attempts
    /// </summary>
    private bool DetectPathTraversal(string path)
    {
        foreach (var pattern in _settings.PathTraversalPatterns)
        {
            if (Regex.IsMatch(path, pattern, RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Validates request headers
    /// </summary>
    private string? ValidateHeaders(HttpRequest request)
    {
        // Check header count
        if (request.Headers.Count > _settings.MaxHeaderCount)
        {
            return $"Excessive header count: {request.Headers.Count} (max: {_settings.MaxHeaderCount})";
        }

        // Check individual header sizes
        foreach (var header in request.Headers)
        {
            var headerSize = Encoding.UTF8.GetByteCount($"{header.Key}: {header.Value}");
            if (headerSize > _settings.MaxHeaderSize)
            {
                return $"Header '{header.Key}' size exceeds limit: {headerSize} bytes (max: {_settings.MaxHeaderSize})";
            }

            // Check for threats in header values
            var threat = DetectThreatsInString(header.Value.ToString());
            if (threat != null)
            {
                return $"Threat in header '{header.Key}': {threat}";
            }
        }

        return null;
    }

    /// <summary>
    /// Checks JSON depth
    /// </summary>
    private bool CheckJsonDepth(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                MaxDepth = _settings.MaxJsonDepth
            });

            return false; // Parsing succeeded, depth is OK
        }
        catch (JsonException ex) when (ex.Message.Contains("depth"))
        {
            return true; // Depth exceeded
        }
        catch
        {
            return false; // Other JSON errors, not depth-related
        }
    }

    /// <summary>
    /// Checks for rapid sequential requests
    /// </summary>
    private async Task<bool> CheckRapidRequestsAsync(IDistributedCache cache, string ipAddress)
    {
        var key = $"{RapidRequestPrefix}{ipAddress}";
        var countStr = await cache.GetStringAsync(key);
        var count = int.TryParse(countStr, out var c) ? c : 0;

        count++;

        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(_settings.RapidRequestWindowSeconds)
        };

        await cache.SetStringAsync(key, count.ToString(), options);

        return count > _settings.RapidRequestThreshold;
    }

    /// <summary>
    /// Logs detected threat to audit log
    /// </summary>
    private async Task LogThreatAsync(string threatType, string? ipAddress, PathString path)
    {
        _logger.LogWarning("SECURITY THREAT DETECTED: Type={ThreatType}, IP={IpAddress}, Path={Path}",
            threatType, ipAddress ?? "unknown", path);

        // In production, also log to security audit log / SIEM
        await Task.CompletedTask;
    }

    /// <summary>
    /// Reads request body as string
    /// </summary>
    private async Task<string> ReadRequestBodyAsync(HttpRequest request)
    {
        if (request.Body == null || request.ContentLength == 0)
        {
            return string.Empty;
        }

        using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
        return await reader.ReadToEndAsync();
    }

    /// <summary>
    /// Gets client IP address
    /// </summary>
    private static string? GetClientIpAddress(HttpContext context)
    {
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            return forwardedFor.Split(',')[0].Trim();
        }

        var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(realIp))
        {
            return realIp;
        }

        return context.Connection.RemoteIpAddress?.ToString();
    }

    /// <summary>
    /// Returns HTTP 403 Forbidden response for blocked threats
    /// </summary>
    private async Task ReturnThreatBlockedResponse(HttpContext context, string message)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(new
        {
            error = "Forbidden",
            message = message,
            incidentId = Guid.NewGuid().ToString()
        });
    }
}

/// <summary>
/// Extension methods for API threat protection middleware
/// </summary>
public static class ApiThreatProtectionMiddlewareExtensions
{
    public static IApplicationBuilder UseApiThreatProtection(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ApiThreatProtectionMiddleware>();
    }
}
