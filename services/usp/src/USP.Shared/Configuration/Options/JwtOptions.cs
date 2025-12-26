using System.ComponentModel.DataAnnotations;

namespace USP.Shared.Configuration.Options;

/// <summary>
/// JWT authentication configuration
/// </summary>
public class JwtOptions
{
    /// <summary>
    /// JWT issuer - ENV: JWT_ISSUER
    /// </summary>
    [Required(ErrorMessage = "JWT Issuer is required")]
    public string Issuer { get; set; } = "security-usp";

    /// <summary>
    /// JWT audience - ENV: JWT_AUDIENCE
    /// </summary>
    [Required(ErrorMessage = "JWT Audience is required")]
    public string Audience { get; set; } = "security-api";

    /// <summary>
    /// Secret key for JWT signing - ENV: JWT_SECRET_KEY
    /// </summary>
    public string? SecretKey { get; set; }

    /// <summary>
    /// Access token expiration in minutes - ENV: JWT_ACCESS_TOKEN_EXPIRY (default: 60)
    /// </summary>
    [Range(5, 1440, ErrorMessage = "JWT expiration must be between 5 and 1440 minutes")]
    public int ExpirationMinutes { get; set; } = 60;

    /// <summary>
    /// Refresh token expiration in days - ENV: JWT_REFRESH_TOKEN_EXPIRY (default: 7)
    /// </summary>
    [Range(1, 30, ErrorMessage = "Refresh token expiration must be between 1 and 30 days")]
    public int RefreshTokenExpirationDays { get; set; } = 7;

    /// <summary>
    /// Algorithm for JWT signing - ENV: JWT_ALGORITHM (default: RS256)
    /// </summary>
    public string Algorithm { get; set; } = "RS256";

    /// <summary>
    /// Path to RSA public key for JWT verification - ENV: JWT_PUBLIC_KEY_PATH
    /// </summary>
    [Required(ErrorMessage = "JWT PublicKeyPath is required for production deployment")]
    public string PublicKeyPath { get; set; } = null!;

    /// <summary>
    /// Path to RSA private key for JWT signing - ENV: JWT_PRIVATE_KEY_PATH
    /// </summary>
    [Required(ErrorMessage = "JWT PrivateKeyPath is required for production deployment")]
    public string PrivateKeyPath { get; set; } = null!;

    /// <summary>
    /// Refresh token length in bytes - ENV: JWT_REFRESH_TOKEN_LENGTH (default: 64)
    /// </summary>
    [Range(32, 128, ErrorMessage = "Refresh token length must be between 32 and 128 bytes")]
    public int RefreshTokenLength { get; set; } = 64;
}
