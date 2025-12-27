using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Security.Claims;
using USP.Core.Domain.Enums;
using USP.Core.Interfaces.Services.Audit;

namespace USP.Infrastructure.Middleware;

/// <summary>
/// Middleware that automatically logs all HTTP requests to the audit service.
/// Captures request details, user information, and response metrics.
/// </summary>
public class AuditMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AuditMiddleware> _logger;

    // Paths to exclude from audit logging (to prevent noise)
    private static readonly HashSet<string> ExcludedPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/health",
        "/health/live",
        "/health/ready",
        "/metrics",
        "/swagger",
        "/swagger/v1/swagger.json"
    };

    public AuditMiddleware(RequestDelegate next, ILogger<AuditMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InvokeAsync(HttpContext context, IAuditService auditService)
    {
        // Skip audit logging for excluded paths
        if (ShouldExcludePath(context.Request.Path))
        {
            await _next(context);
            return;
        }

        // Start timing
        var stopwatch = Stopwatch.StartNew();

        // Extract request information
        var userId = context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        var userName = context.User?.Identity?.Name;
        var ipAddress = GetClientIpAddress(context);
        var userAgent = context.Request.Headers["User-Agent"].ToString();
        var correlationId = GetOrCreateCorrelationId(context);
        var method = context.Request.Method;
        var path = context.Request.Path;
        var queryString = context.Request.QueryString.ToString();

        // Build resource path
        var resource = $"{method} {path}";

        // Store correlation ID in context for downstream usage
        context.Items["CorrelationId"] = correlationId;

        // Capture original body stream
        var originalBodyStream = context.Response.Body;
        var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        Exception? exception = null;
        int statusCode = 0;

        try
        {
            // Continue pipeline
            await _next(context);
            statusCode = context.Response.StatusCode;
        }
        catch (Exception ex)
        {
            exception = ex;
            statusCode = 500;
            throw;
        }
        finally
        {
            stopwatch.Stop();

            // Determine if request was successful
            var success = statusCode >= 200 && statusCode < 400;

            // Determine event type based on path and method
            var eventType = DetermineEventType(path, method);

            // Build audit details
            var details = BuildAuditDetails(
                method,
                path,
                queryString,
                statusCode,
                stopwatch.ElapsedMilliseconds,
                exception);

            // Log to audit service asynchronously (don't await to avoid blocking)
            _ = Task.Run(async () =>
            {
                try
                {
                    await auditService.LogEventAsync(
                        eventType,
                        userId,
                        userName,
                        resource,
                        action: method,
                        success,
                        ipAddress,
                        userAgent,
                        details,
                        correlationId,
                        cancellationToken: default);
                }
                catch (Exception auditEx)
                {
                    // Log audit failure but don't throw (audit shouldn't break requests)
                    _logger.LogError(auditEx, "Failed to log audit event for {Resource}", resource);
                }
            });

            // Copy response body back to original stream
            responseBody.Seek(0, SeekOrigin.Begin);
            await responseBody.CopyToAsync(originalBodyStream);
            context.Response.Body = originalBodyStream;
        }
    }

    /// <summary>
    /// Determines if the path should be excluded from audit logging
    /// </summary>
    private bool ShouldExcludePath(PathString path)
    {
        if (path.Value == null)
            return false;

        // Exact match
        if (ExcludedPaths.Contains(path.Value))
            return true;

        // Prefix match for Swagger UI
        if (path.Value.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    /// <summary>
    /// Gets or creates a correlation ID for the request
    /// </summary>
    private string GetOrCreateCorrelationId(HttpContext context)
    {
        // Check if correlation ID exists in headers (from client or upstream service)
        if (context.Request.Headers.TryGetValue("X-Correlation-ID", out var correlationId) &&
            !string.IsNullOrEmpty(correlationId))
        {
            return correlationId.ToString();
        }

        // Check trace identifier
        if (!string.IsNullOrEmpty(context.TraceIdentifier))
        {
            return context.TraceIdentifier;
        }

        // Generate new correlation ID
        return Guid.NewGuid().ToString();
    }

    /// <summary>
    /// Gets the client IP address from the request, considering proxies
    /// </summary>
    private string? GetClientIpAddress(HttpContext context)
    {
        // Check X-Forwarded-For header (set by proxies/load balancers)
        if (context.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor))
        {
            var ip = forwardedFor.ToString().Split(',').FirstOrDefault()?.Trim();
            if (!string.IsNullOrEmpty(ip))
                return ip;
        }

        // Check X-Real-IP header (set by some proxies)
        if (context.Request.Headers.TryGetValue("X-Real-IP", out var realIp))
        {
            var ip = realIp.ToString();
            if (!string.IsNullOrEmpty(ip))
                return ip;
        }

        // Fall back to connection remote IP
        return context.Connection.RemoteIpAddress?.ToString();
    }

    /// <summary>
    /// Determines the audit event type based on the request path and method
    /// </summary>
    private AuditEventType DetermineEventType(PathString path, string method)
    {
        var pathValue = path.Value?.ToLowerInvariant() ?? "";

        // Authentication events
        if (pathValue.Contains("/auth/login"))
            return AuditEventType.UserLogin;
        if (pathValue.Contains("/auth/logout"))
            return AuditEventType.UserLogout;
        if (pathValue.Contains("/auth/register"))
            return AuditEventType.UserCreated;
        if (pathValue.Contains("/auth/change-password"))
            return AuditEventType.PasswordChanged;
        if (pathValue.Contains("/mfa"))
            return AuditEventType.MFAEnabled;

        // Secret events
        if (pathValue.Contains("/secrets"))
        {
            return method.ToUpperInvariant() switch
            {
                "POST" => AuditEventType.SecretWritten,
                "GET" => AuditEventType.SecretRead,
                "DELETE" => AuditEventType.SecretDeleted,
                "PUT" => AuditEventType.SecretUpdated,
                _ => AuditEventType.ApiRequest
            };
        }

        // Authorization events
        if (pathValue.Contains("/authz") || pathValue.Contains("/roles") || pathValue.Contains("/policies"))
        {
            return AuditEventType.PolicyCreated;
        }

        // PAM events
        if (pathValue.Contains("/pam/checkout"))
            return AuditEventType.AccountCheckedOut;
        if (pathValue.Contains("/pam/checkin"))
            return AuditEventType.AccountCheckedIn;

        // Rotation events
        if (pathValue.Contains("/rotation"))
            return AuditEventType.SecretRotated;

        // Audit events
        if (pathValue.Contains("/audit"))
            return AuditEventType.AuditLogExported;

        // Default to generic API request
        return AuditEventType.ApiRequest;
    }

    /// <summary>
    /// Builds detailed audit information as JSON
    /// </summary>
    private string BuildAuditDetails(
        string method,
        PathString path,
        QueryString queryString,
        int statusCode,
        long durationMs,
        Exception? exception)
    {
        var details = new
        {
            request = new
            {
                method,
                path = path.Value,
                queryString = queryString.Value
            },
            response = new
            {
                statusCode,
                durationMs
            },
            error = exception != null ? new
            {
                message = exception.Message,
                type = exception.GetType().Name
            } : null
        };

        return System.Text.Json.JsonSerializer.Serialize(details);
    }
}
