namespace USP.Core.Models.Configuration;

/// <summary>
/// Request signing configuration settings
/// </summary>
public class RequestSigningSettings
{
    /// <summary>
    /// Enable request signature verification
    /// </summary>
    public bool EnableSignatureVerification { get; set; } = true;

    /// <summary>
    /// Maximum allowed timestamp drift in seconds
    /// </summary>
    public int MaxTimestampDriftSeconds { get; set; } = 300;

    /// <summary>
    /// Signature header name
    /// </summary>
    public string SignatureHeader { get; set; } = "X-Signature";

    /// <summary>
    /// Timestamp header name
    /// </summary>
    public string TimestampHeader { get; set; } = "X-Timestamp";

    /// <summary>
    /// Nonce header name (for replay attack prevention)
    /// </summary>
    public string NonceHeader { get; set; } = "X-Nonce";

    /// <summary>
    /// API key ID header name (to identify which secret to use)
    /// </summary>
    public string ApiKeyIdHeader { get; set; } = "X-Api-Key-Id";

    /// <summary>
    /// Nonce expiration time in seconds (how long nonces are stored in cache)
    /// </summary>
    public int NonceExpirationSeconds { get; set; } = 600;

    /// <summary>
    /// Hash algorithm (HMACSHA256, HMACSHA384, HMACSHA512)
    /// </summary>
    public string HashAlgorithm { get; set; } = "HMACSHA256";

    /// <summary>
    /// Endpoints that require request signing (if empty, all endpoints require it)
    /// </summary>
    public List<string> RequiredSigningEndpoints { get; set; } = new();

    /// <summary>
    /// Endpoints that are exempt from request signing
    /// </summary>
    public List<string> ExemptSigningEndpoints { get; set; } = new()
    {
        "/health",
        "/health/live",
        "/health/ready",
        "/swagger",
        "/api/auth/login",
        "/api/auth/register"
    };

    /// <summary>
    /// Validates request signing configuration settings
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when validation fails</exception>
    public void Validate()
    {
        if (MaxTimestampDriftSeconds <= 0)
        {
            throw new InvalidOperationException("Max timestamp drift seconds must be positive");
        }

        if (NonceExpirationSeconds <= 0)
        {
            throw new InvalidOperationException("Nonce expiration seconds must be positive");
        }

        if (string.IsNullOrWhiteSpace(SignatureHeader))
        {
            throw new InvalidOperationException("Signature header cannot be empty");
        }

        if (string.IsNullOrWhiteSpace(TimestampHeader))
        {
            throw new InvalidOperationException("Timestamp header cannot be empty");
        }

        if (string.IsNullOrWhiteSpace(NonceHeader))
        {
            throw new InvalidOperationException("Nonce header cannot be empty");
        }

        var supportedAlgorithms = new[] { "HMACSHA256", "HMACSHA384", "HMACSHA512" };
        if (!supportedAlgorithms.Contains(HashAlgorithm, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Unsupported hash algorithm: {HashAlgorithm}. Supported: {string.Join(", ", supportedAlgorithms)}");
        }
    }
}
