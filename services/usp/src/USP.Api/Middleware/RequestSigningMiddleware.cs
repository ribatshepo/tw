using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using USP.Core.Models.Configuration;
using USP.Core.Services.ApiKey;

namespace USP.Api.Middleware;

/// <summary>
/// Request signing middleware for HMAC signature verification
/// Prevents replay attacks using nonces and timestamp validation
/// </summary>
public class RequestSigningMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestSigningMiddleware> _logger;
    private readonly RequestSigningSettings _settings;
    private readonly IServiceProvider _serviceProvider;

    private const string NoncePrefix = "request:nonce:";

    public RequestSigningMiddleware(
        RequestDelegate next,
        ILogger<RequestSigningMiddleware> logger,
        IOptions<RequestSigningSettings> settings,
        IServiceProvider serviceProvider)
    {
        _next = next;
        _logger = logger;
        _settings = settings.Value;
        _serviceProvider = serviceProvider;
    }

    public async Task InvokeAsync(HttpContext context, IDistributedCache cache)
    {
        // Skip signature verification if disabled or for exempt endpoints
        if (!_settings.EnableSignatureVerification || IsExemptEndpoint(context.Request.Path))
        {
            await _next(context);
            return;
        }

        try
        {
            // Extract signature headers
            var signature = context.Request.Headers[_settings.SignatureHeader].FirstOrDefault();
            var timestamp = context.Request.Headers[_settings.TimestampHeader].FirstOrDefault();
            var nonce = context.Request.Headers[_settings.NonceHeader].FirstOrDefault();
            var apiKeyId = context.Request.Headers[_settings.ApiKeyIdHeader].FirstOrDefault();

            // Check if signature is required for this endpoint
            var requiresSignature = RequiresSignature(context.Request.Path);

            if (requiresSignature)
            {
                if (string.IsNullOrEmpty(signature) || string.IsNullOrEmpty(timestamp) || string.IsNullOrEmpty(nonce))
                {
                    _logger.LogWarning("Missing required signature headers for {Path}", context.Request.Path);
                    await ReturnUnauthorizedResponse(context, "Missing required signature headers");
                    return;
                }

                // Validate timestamp
                if (!ValidateTimestamp(timestamp, out var parsedTimestamp))
                {
                    _logger.LogWarning("Invalid or expired timestamp: {Timestamp}", timestamp);
                    await ReturnUnauthorizedResponse(context, "Invalid or expired timestamp");
                    return;
                }

                // Check for replay attack (nonce reuse)
                if (await IsNonceUsed(cache, nonce))
                {
                    _logger.LogWarning("Replay attack detected: Nonce {Nonce} already used", nonce);
                    await ReturnUnauthorizedResponse(context, "Request replay detected");
                    return;
                }

                // Read request body
                context.Request.EnableBuffering();
                var body = await ReadRequestBodyAsync(context.Request);

                // Verify signature
                var isValid = await VerifySignatureAsync(
                    signature,
                    context.Request.Method,
                    context.Request.Path.Value ?? string.Empty,
                    timestamp,
                    nonce,
                    body,
                    apiKeyId
                );

                if (!isValid)
                {
                    _logger.LogWarning("Invalid request signature for {Method} {Path}", context.Request.Method, context.Request.Path);
                    await ReturnUnauthorizedResponse(context, "Invalid request signature");
                    return;
                }

                // Store nonce to prevent replay
                await StoreNonce(cache, nonce);

                _logger.LogDebug("Request signature verified successfully for {Method} {Path}", context.Request.Method, context.Request.Path);

                // Reset body stream position
                context.Request.Body.Position = 0;
            }

            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during request signature verification");

            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "Internal server error",
                message = "An error occurred during request verification"
            });
        }
    }

    /// <summary>
    /// Verifies HMAC signature
    /// </summary>
    private async Task<bool> VerifySignatureAsync(
        string providedSignature,
        string method,
        string path,
        string timestamp,
        string nonce,
        string body,
        string? apiKeyId)
    {
        try
        {
            // Get secret key for signature verification
            var secret = await GetSigningSecretAsync(apiKeyId);

            if (string.IsNullOrEmpty(secret))
            {
                _logger.LogWarning("Unable to retrieve signing secret for API key: {ApiKeyId}", apiKeyId);
                return false;
            }

            // Build signature string: METHOD + PATH + TIMESTAMP + NONCE + BODY
            var signatureString = $"{method}{path}{timestamp}{nonce}{body}";

            // Compute HMAC
            var computedSignature = ComputeHmacSignature(signatureString, secret);

            // Compare signatures (constant-time comparison to prevent timing attacks)
            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(providedSignature),
                Encoding.UTF8.GetBytes(computedSignature)
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error computing signature");
            return false;
        }
    }

    /// <summary>
    /// Computes HMAC signature
    /// </summary>
    private string ComputeHmacSignature(string data, string secret)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var dataBytes = Encoding.UTF8.GetBytes(data);

        HMAC hmac = _settings.HashAlgorithm.ToUpperInvariant() switch
        {
            "HMACSHA256" => new HMACSHA256(keyBytes),
            "HMACSHA384" => new HMACSHA384(keyBytes),
            "HMACSHA512" => new HMACSHA512(keyBytes),
            _ => new HMACSHA256(keyBytes)
        };

        using (hmac)
        {
            var hashBytes = hmac.ComputeHash(dataBytes);
            return Convert.ToBase64String(hashBytes);
        }
    }

    /// <summary>
    /// Validates timestamp (rejects requests older than configured drift)
    /// </summary>
    private bool ValidateTimestamp(string timestamp, out DateTimeOffset parsedTimestamp)
    {
        parsedTimestamp = DateTimeOffset.MinValue;

        // Try parsing as Unix timestamp (seconds)
        if (long.TryParse(timestamp, out var unixSeconds))
        {
            parsedTimestamp = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
        }
        // Try parsing as ISO 8601
        else if (DateTimeOffset.TryParse(timestamp, out var dateTime))
        {
            parsedTimestamp = dateTime;
        }
        else
        {
            return false;
        }

        var now = DateTimeOffset.UtcNow;
        var diff = Math.Abs((now - parsedTimestamp).TotalSeconds);

        return diff <= _settings.MaxTimestampDriftSeconds;
    }

    /// <summary>
    /// Checks if nonce has been used (replay attack detection)
    /// </summary>
    private async Task<bool> IsNonceUsed(IDistributedCache cache, string nonce)
    {
        var key = $"{NoncePrefix}{nonce}";
        var value = await cache.GetStringAsync(key);
        return !string.IsNullOrEmpty(value);
    }

    /// <summary>
    /// Stores nonce in cache to prevent replay
    /// </summary>
    private async Task StoreNonce(IDistributedCache cache, string nonce)
    {
        var key = $"{NoncePrefix}{nonce}";
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(_settings.NonceExpirationSeconds)
        };

        await cache.SetStringAsync(key, "1", options);
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
    /// Retrieves signing secret for API key
    /// </summary>
    private async Task<string> GetSigningSecretAsync(string? apiKeyId)
    {
        if (string.IsNullOrEmpty(apiKeyId))
        {
            // Use default signing secret from configuration
            return Environment.GetEnvironmentVariable("USP_REQUEST_SIGNING_SECRET") ?? string.Empty;
        }

        // Retrieve API key with signing secret from database
        using var scope = _serviceProvider.CreateScope();
        var apiKeyService = scope.ServiceProvider.GetRequiredService<IApiKeyManagementService>();

        var apiKey = await apiKeyService.GetApiKeyAsync(apiKeyId);

        if (apiKey == null)
        {
            _logger.LogWarning("API key {ApiKeyId} not found", apiKeyId);
            return string.Empty;
        }

        if (!apiKey.IsActive)
        {
            _logger.LogWarning("API key {ApiKeyId} is inactive (revoked or expired)", apiKeyId);
            return string.Empty;
        }

        return apiKey.SigningSecret ?? string.Empty;
    }

    /// <summary>
    /// Checks if endpoint is exempt from signature verification
    /// </summary>
    private bool IsExemptEndpoint(PathString path)
    {
        return _settings.ExemptSigningEndpoints.Any(endpoint =>
            path.StartsWithSegments(endpoint, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Checks if endpoint requires signature
    /// </summary>
    private bool RequiresSignature(PathString path)
    {
        // If specific endpoints are configured, only those require signing
        if (_settings.RequiredSigningEndpoints.Any())
        {
            return _settings.RequiredSigningEndpoints.Any(endpoint =>
                path.StartsWithSegments(endpoint, StringComparison.OrdinalIgnoreCase));
        }

        // Otherwise, all non-exempt endpoints require signing
        return !IsExemptEndpoint(path);
    }

    /// <summary>
    /// Returns HTTP 401 Unauthorized response
    /// </summary>
    private async Task ReturnUnauthorizedResponse(HttpContext context, string message)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsJsonAsync(new
        {
            error = "Unauthorized",
            message = message,
            hint = "Ensure request includes valid X-Signature, X-Timestamp, and X-Nonce headers"
        });
    }
}

/// <summary>
/// Extension methods for request signing middleware
/// </summary>
public static class RequestSigningMiddlewareExtensions
{
    public static IApplicationBuilder UseRequestSigning(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<RequestSigningMiddleware>();
    }
}
