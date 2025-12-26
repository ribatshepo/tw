namespace USP.Core.Models.Configuration;

/// <summary>
/// JWT authentication configuration settings
/// </summary>
public class JwtSettings
{
    /// <summary>
    /// JWT signing algorithm (HS256, RS256, etc.)
    /// </summary>
    public string Algorithm { get; set; } = "HS256";

    /// <summary>
    /// JWT secret key for HMAC algorithms (HS256, HS384, HS512)
    /// MUST be loaded from secure configuration (User Secrets or environment variables)
    /// </summary>
    public string Secret { get; set; } = string.Empty;

    /// <summary>
    /// Path to RSA private key file for RSA algorithms (RS256, RS384, RS512)
    /// </summary>
    public string PrivateKeyPath { get; set; } = string.Empty;

    /// <summary>
    /// JWT issuer claim (iss)
    /// </summary>
    public string Issuer { get; set; } = string.Empty;

    /// <summary>
    /// JWT audience claim (aud)
    /// </summary>
    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// Access token expiration time in minutes
    /// </summary>
    public int AccessTokenExpirationMinutes { get; set; } = 15;

    /// <summary>
    /// Refresh token expiration time in days
    /// </summary>
    public int RefreshTokenExpirationDays { get; set; } = 7;

    /// <summary>
    /// Validates JWT configuration settings
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when validation fails</exception>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Algorithm))
        {
            throw new InvalidOperationException("JWT algorithm is required. Set Jwt:Algorithm in configuration.");
        }

        var hmacAlgorithms = new[] { "HS256", "HS384", "HS512" };
        var rsaAlgorithms = new[] { "RS256", "RS384", "RS512" };

        if (hmacAlgorithms.Contains(Algorithm, StringComparer.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(Secret))
            {
                throw new InvalidOperationException(
                    $"JWT secret is required for {Algorithm} algorithm. Set Jwt:Secret in User Secrets (development) or environment variable (production). " +
                    "For development: dotnet user-secrets set \"Jwt:Secret\" \"your-secret-min-32-chars\"");
            }

            if (Secret.Length < 32)
            {
                throw new InvalidOperationException(
                    $"JWT secret must be at least 32 characters long for security. Current length: {Secret.Length} characters. " +
                    "Generate a strong secret: openssl rand -base64 64");
            }
        }
        else if (rsaAlgorithms.Contains(Algorithm, StringComparer.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(PrivateKeyPath))
            {
                throw new InvalidOperationException(
                    $"Private key path is required for {Algorithm} algorithm. Set Jwt:PrivateKeyPath in configuration.");
            }

            if (!File.Exists(PrivateKeyPath))
            {
                throw new InvalidOperationException(
                    $"Private key file not found at path: {PrivateKeyPath}. " +
                    "Generate RSA key pair: openssl genrsa -out private.pem 4096");
            }
        }
        else
        {
            throw new InvalidOperationException(
                $"Unsupported JWT algorithm: {Algorithm}. Supported algorithms: {string.Join(", ", hmacAlgorithms.Concat(rsaAlgorithms))}");
        }

        if (string.IsNullOrWhiteSpace(Issuer))
        {
            throw new InvalidOperationException("JWT issuer is required. Set Jwt:Issuer in configuration.");
        }

        if (string.IsNullOrWhiteSpace(Audience))
        {
            throw new InvalidOperationException("JWT audience is required. Set Jwt:Audience in configuration.");
        }

        if (AccessTokenExpirationMinutes <= 0)
        {
            throw new InvalidOperationException(
                $"Access token expiration must be positive. Current value: {AccessTokenExpirationMinutes} minutes. " +
                "Recommended: 15-60 minutes");
        }

        if (RefreshTokenExpirationDays <= 0)
        {
            throw new InvalidOperationException(
                $"Refresh token expiration must be positive. Current value: {RefreshTokenExpirationDays} days. " +
                "Recommended: 7-30 days");
        }

        if (AccessTokenExpirationMinutes > 1440)
        {
            throw new InvalidOperationException(
                $"Access token expiration is too long ({AccessTokenExpirationMinutes} minutes = {AccessTokenExpirationMinutes / 60} hours). " +
                "Security best practice: Keep access tokens short-lived (15-60 minutes)");
        }
    }
}
