using System.Security.Claims;
using USP.Core.Services.ApiKey;

namespace USP.Api.Middleware;

/// <summary>
/// Middleware for API key authentication
/// Checks for API key in Authorization header: "ApiKey {key}" or X-API-Key header
/// </summary>
public class ApiKeyAuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiKeyAuthenticationMiddleware> _logger;

    public ApiKeyAuthenticationMiddleware(
        RequestDelegate next,
        ILogger<ApiKeyAuthenticationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IApiKeyManagementService apiKeyService)
    {
        // Skip API key authentication for certain paths
        if (ShouldSkipAuthentication(context.Request.Path))
        {
            await _next(context);
            return;
        }

        // Try to extract API key from headers
        var apiKey = ExtractApiKey(context.Request);

        if (!string.IsNullOrEmpty(apiKey))
        {
            try
            {
                var validationResult = await apiKeyService.ValidateApiKeyAsync(apiKey);

                if (validationResult.HasValue)
                {
                    var (userId, scopes, apiKeyId) = validationResult.Value;

                    // Check rate limit
                    var withinRateLimit = await apiKeyService.CheckRateLimitAsync(apiKeyId);

                    if (!withinRateLimit)
                    {
                        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                        await context.Response.WriteAsJsonAsync(new
                        {
                            error = "Rate limit exceeded",
                            message = "API key rate limit has been exceeded. Please try again later."
                        });
                        return;
                    }

                    // Record usage (fire and forget)
                    var endpoint = $"{context.Request.Method} {context.Request.Path}";
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await apiKeyService.RecordUsageAsync(apiKeyId, endpoint);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error recording API key usage");
                        }
                    });

                    // Set user context
                    var claims = new List<Claim>
                    {
                        new(ClaimTypes.NameIdentifier, userId.ToString()),
                        new("api_key_id", apiKeyId.ToString()),
                        new("auth_type", "apikey")
                    };

                    // Add scopes as claims
                    foreach (var scope in scopes)
                    {
                        claims.Add(new Claim("scope", scope));
                    }

                    var identity = new ClaimsIdentity(claims, "ApiKey");
                    context.User = new ClaimsPrincipal(identity);

                    _logger.LogDebug("API key authenticated for user {UserId}", userId);
                }
                else
                {
                    _logger.LogWarning("Invalid API key attempt from {IpAddress}", context.Connection.RemoteIpAddress);

                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    await context.Response.WriteAsJsonAsync(new
                    {
                        error = "Unauthorized",
                        message = "Invalid or expired API key"
                    });
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during API key authentication");

                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await context.Response.WriteAsJsonAsync(new
                {
                    error = "Internal server error",
                    message = "An error occurred during authentication"
                });
                return;
            }
        }

        await _next(context);
    }

    private static string? ExtractApiKey(HttpRequest request)
    {
        // Try Authorization header with "ApiKey" scheme
        var authHeader = request.Headers.Authorization.ToString();
        if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("ApiKey ", StringComparison.OrdinalIgnoreCase))
        {
            return authHeader.Substring(7).Trim();
        }

        // Try X-API-Key header
        if (request.Headers.TryGetValue("X-API-Key", out var apiKeyHeader))
        {
            return apiKeyHeader.ToString();
        }

        return null;
    }

    private static bool ShouldSkipAuthentication(PathString path)
    {
        var skipPaths = new[]
        {
            "/health",
            "/health/live",
            "/health/ready",
            "/swagger",
            "/api/auth/login",
            "/api/auth/register",
            "/api/auth/refresh"
        };

        return skipPaths.Any(p => path.StartsWithSegments(p, StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>
/// Extension methods for API key authentication middleware
/// </summary>
public static class ApiKeyAuthenticationMiddlewareExtensions
{
    public static IApplicationBuilder UseApiKeyAuthentication(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ApiKeyAuthenticationMiddleware>();
    }
}
