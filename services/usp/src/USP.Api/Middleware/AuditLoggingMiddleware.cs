using System.Diagnostics;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using USP.Core.Models.Entities;
using USP.Infrastructure.Data;

namespace USP.Api.Middleware;

/// <summary>
/// Middleware for comprehensive audit logging of API requests
/// </summary>
public class AuditLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AuditLoggingMiddleware> _logger;

    public AuditLoggingMiddleware(
        RequestDelegate next,
        ILogger<AuditLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ApplicationDbContext dbContext)
    {
        // Skip audit logging for certain paths
        if (ShouldSkipAudit(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        var originalBodyStream = context.Response.Body;

        using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        // Capture request details
        var requestBody = await CaptureRequestBodyAsync(context.Request);

        Exception? exception = null;
        int statusCode = 0;

        try
        {
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

            // Capture response
            var responseBodyText = await CaptureResponseBodyAsync(responseBody);

            // Copy response back to original stream
            responseBody.Seek(0, SeekOrigin.Begin);
            await responseBody.CopyToAsync(originalBodyStream);

            // Create audit log entry
            try
            {
                await CreateAuditLogAsync(
                    dbContext,
                    context,
                    requestBody,
                    responseBodyText,
                    statusCode,
                    stopwatch.ElapsedMilliseconds,
                    exception);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create audit log entry");
            }
        }
    }

    private static bool ShouldSkipAudit(PathString path)
    {
        var pathValue = path.Value?.ToLower() ?? string.Empty;

        // Skip health checks, metrics, and static files
        return pathValue.StartsWith("/health") ||
               pathValue.StartsWith("/metrics") ||
               pathValue.StartsWith("/swagger") ||
               pathValue.StartsWith("/_") ||
               pathValue.Contains(".css") ||
               pathValue.Contains(".js") ||
               pathValue.Contains(".map");
    }

    private static async Task<string?> CaptureRequestBodyAsync(HttpRequest request)
    {
        if (!request.ContentLength.HasValue || request.ContentLength.Value == 0)
        {
            return null;
        }

        // Skip capturing body for sensitive endpoints
        if (IsSensitiveEndpoint(request.Path))
        {
            return "[REDACTED]";
        }

        request.EnableBuffering();

        using var reader = new StreamReader(
            request.Body,
            encoding: Encoding.UTF8,
            detectEncodingFromByteOrderMarks: false,
            bufferSize: 1024,
            leaveOpen: true);

        var body = await reader.ReadToEndAsync();
        request.Body.Position = 0;

        // Redact sensitive fields
        return RedactSensitiveData(body);
    }

    private static async Task<string?> CaptureResponseBodyAsync(MemoryStream responseBody)
    {
        responseBody.Seek(0, SeekOrigin.Begin);
        var text = await new StreamReader(responseBody).ReadToEndAsync();
        responseBody.Seek(0, SeekOrigin.Begin);

        return text.Length > 5000 ? text[..5000] + "... [truncated]" : text;
    }

    private static bool IsSensitiveEndpoint(PathString path)
    {
        var pathValue = path.Value?.ToLower() ?? string.Empty;
        return pathValue.Contains("/login") ||
               pathValue.Contains("/register") ||
               pathValue.Contains("/password") ||
               pathValue.Contains("/secret") ||
               pathValue.Contains("/token");
    }

    private static string RedactSensitiveData(string body)
    {
        try
        {
            var json = JsonDocument.Parse(body);
            var options = new JsonSerializerOptions { WriteIndented = false };

            var redactedJson = RedactSensitiveFields(json.RootElement);
            return JsonSerializer.Serialize(redactedJson, options);
        }
        catch
        {
            return body.Length > 1000 ? body[..1000] + "... [truncated]" : body;
        }
    }

    private static object RedactSensitiveFields(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var obj = new Dictionary<string, object>();
                foreach (var property in element.EnumerateObject())
                {
                    var propName = property.Name.ToLower();
                    if (propName.Contains("password") ||
                        propName.Contains("secret") ||
                        propName.Contains("token") ||
                        propName.Contains("key") ||
                        propName.Contains("credential"))
                    {
                        obj[property.Name] = "[REDACTED]";
                    }
                    else
                    {
                        obj[property.Name] = RedactSensitiveFields(property.Value);
                    }
                }
                return obj;

            case JsonValueKind.Array:
                return element.EnumerateArray()
                    .Select(RedactSensitiveFields)
                    .ToList();

            case JsonValueKind.String:
                return element.GetString() ?? string.Empty;

            case JsonValueKind.Number:
                return element.TryGetInt64(out var longValue) ? longValue : element.GetDouble();

            case JsonValueKind.True:
                return true;

            case JsonValueKind.False:
                return false;

            case JsonValueKind.Null:
                return "null";

            default:
                return element.ToString();
        }
    }

    private async Task CreateAuditLogAsync(
        ApplicationDbContext dbContext,
        HttpContext context,
        string? requestBody,
        string? responseBody,
        int statusCode,
        long durationMs,
        Exception? exception)
    {
        var userId = GetUserId(context.User);
        var username = context.User.Identity?.Name ?? "anonymous";

        var auditLog = new AuditLog
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            UserId = userId,
            Username = username,
            Action = DetermineAction(context),
            Resource = DetermineResource(context),
            ResourceId = ExtractResourceId(context),
            IpAddress = GetIpAddress(context),
            UserAgent = context.Request.Headers.UserAgent.ToString(),
            HttpMethod = context.Request.Method,
            RequestPath = context.Request.Path,
            QueryString = context.Request.QueryString.ToString(),
            RequestBody = requestBody,
            ResponseStatus = statusCode,
            ResponseBody = statusCode >= 400 ? responseBody : null, // Only log response for errors
            Success = statusCode < 400 && exception == null,
            ErrorMessage = exception?.Message,
            DurationMs = (int)durationMs,
            Metadata = BuildMetadata(context, exception)
        };

        dbContext.AuditLogs.Add(auditLog);

        try
        {
            await dbContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save audit log to database");

            // Log to file as fallback
            _logger.LogWarning(
                "Audit Log (fallback): User={Username}, Action={Action}, Resource={Resource}, Status={Status}, Duration={Duration}ms",
                username, auditLog.Action, auditLog.Resource, statusCode, durationMs);
        }
    }

    private static Guid? GetUserId(ClaimsPrincipal user)
    {
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }

    private static string GetIpAddress(HttpContext context)
    {
        // Check for forwarded IP first (behind proxy/load balancer)
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            return forwardedFor.Split(',')[0].Trim();
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    private static string DetermineAction(HttpContext context)
    {
        var path = context.Request.Path.Value?.ToLower() ?? string.Empty;
        var method = context.Request.Method.ToUpper();

        // Authentication actions
        if (path.Contains("/auth/login")) return "Login";
        if (path.Contains("/auth/logout")) return "Logout";
        if (path.Contains("/auth/register")) return "Register";
        if (path.Contains("/auth/refresh")) return "RefreshToken";

        // Role actions
        if (path.Contains("/roles"))
        {
            return method switch
            {
                "GET" => "ViewRole",
                "POST" => path.Contains("/permissions") ? "AssignPermissions" : "CreateRole",
                "PUT" => "UpdateRole",
                "DELETE" => path.Contains("/permissions") ? "RemovePermissions" : "DeleteRole",
                _ => "RoleOperation"
            };
        }

        // Secret actions
        if (path.Contains("/secret"))
        {
            return method switch
            {
                "GET" => "ReadSecret",
                "POST" => "CreateSecret",
                "PUT" => "UpdateSecret",
                "DELETE" => "DeleteSecret",
                _ => "SecretOperation"
            };
        }

        // PAM actions
        if (path.Contains("/pam"))
        {
            if (path.Contains("/checkout")) return "CheckoutCredential";
            if (path.Contains("/checkin")) return "CheckinCredential";
            if (path.Contains("/safe")) return "ManageSafe";
            return "PAMOperation";
        }

        // Generic CRUD
        return method switch
        {
            "GET" => "Read",
            "POST" => "Create",
            "PUT" => "Update",
            "PATCH" => "Patch",
            "DELETE" => "Delete",
            _ => "Unknown"
        };
    }

    private static string DetermineResource(HttpContext context)
    {
        var path = context.Request.Path.Value?.ToLower() ?? string.Empty;

        if (path.Contains("/auth")) return "Authentication";
        if (path.Contains("/roles")) return "Role";
        if (path.Contains("/users")) return "User";
        if (path.Contains("/secret")) return "Secret";
        if (path.Contains("/pam")) return "PAM";
        if (path.Contains("/policy")) return "Policy";
        if (path.Contains("/audit")) return "AuditLog";
        if (path.Contains("/workspace")) return "Workspace";

        return "Unknown";
    }

    private static string? ExtractResourceId(HttpContext context)
    {
        var pathSegments = context.Request.Path.Value?
            .Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (pathSegments == null || pathSegments.Length < 3)
            return null;

        // Try to find a GUID in the path
        foreach (var segment in pathSegments)
        {
            if (Guid.TryParse(segment, out _))
            {
                return segment;
            }
        }

        return null;
    }

    private static string BuildMetadata(HttpContext context, Exception? exception)
    {
        var metadata = new Dictionary<string, object>
        {
            ["host"] = context.Request.Host.ToString(),
            ["scheme"] = context.Request.Scheme,
            ["protocol"] = context.Request.Protocol,
            ["contentType"] = context.Request.ContentType ?? "none",
            ["contentLength"] = context.Request.ContentLength ?? 0
        };

        if (exception != null)
        {
            metadata["exceptionType"] = exception.GetType().Name;
            metadata["stackTrace"] = exception.StackTrace ?? "No stack trace";
        }

        var traceId = context.TraceIdentifier;
        if (!string.IsNullOrEmpty(traceId))
        {
            metadata["traceId"] = traceId;
        }

        return JsonSerializer.Serialize(metadata);
    }
}
