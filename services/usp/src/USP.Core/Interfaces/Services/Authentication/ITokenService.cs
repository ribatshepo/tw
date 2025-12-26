using System.Security.Claims;

namespace USP.Core.Interfaces.Services.Authentication;

/// <summary>
/// Provides JWT token generation, validation, and refresh token operations.
/// </summary>
public interface ITokenService
{
    /// <summary>
    /// Generates a JWT access token using RS256 signing.
    /// </summary>
    /// <param name="userId">The user ID to include in claims</param>
    /// <param name="email">The user's email</param>
    /// <param name="roles">The user's roles</param>
    /// <param name="additionalClaims">Optional additional claims to include</param>
    /// <returns>JWT access token</returns>
    Task<string> GenerateAccessTokenAsync(
        string userId,
        string email,
        IEnumerable<string> roles,
        IDictionary<string, string>? additionalClaims = null);

    /// <summary>
    /// Generates a cryptographically secure refresh token.
    /// </summary>
    /// <returns>Refresh token (Base64 encoded)</returns>
    string GenerateRefreshToken();

    /// <summary>
    /// Validates a JWT token signature, expiration, issuer, and audience.
    /// </summary>
    /// <param name="token">The JWT token to validate</param>
    /// <returns>Validation result with principal if valid</returns>
    Task<TokenValidationResult> ValidateTokenAsync(string token);

    /// <summary>
    /// Extracts claims from a JWT token without validating expiration.
    /// </summary>
    /// <param name="token">The JWT token</param>
    /// <returns>Claims principal</returns>
    ClaimsPrincipal? ExtractClaimsWithoutValidation(string token);

    /// <summary>
    /// Gets the expiration time for access tokens in seconds.
    /// </summary>
    int GetAccessTokenExpirationSeconds();
}

/// <summary>
/// Result of token validation.
/// </summary>
public class TokenValidationResult
{
    public bool IsValid { get; set; }
    public ClaimsPrincipal? Principal { get; set; }
    public string? Error { get; set; }
}
