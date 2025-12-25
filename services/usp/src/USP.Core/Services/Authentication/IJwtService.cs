using System.Security.Claims;
using USP.Core.Models.Entities;

namespace USP.Core.Services.Authentication;

/// <summary>
/// Service for JWT token generation and validation
/// </summary>
public interface IJwtService
{
    /// <summary>
    /// Generate access token for user
    /// </summary>
    string GenerateAccessToken(ApplicationUser user, IEnumerable<string> roles);

    /// <summary>
    /// Generate refresh token
    /// </summary>
    string GenerateRefreshToken();

    /// <summary>
    /// Validate JWT token and extract claims
    /// </summary>
    ClaimsPrincipal? ValidateToken(string token);

    /// <summary>
    /// Get user ID from claims
    /// </summary>
    Guid? GetUserIdFromClaims(ClaimsPrincipal principal);

    /// <summary>
    /// Hash token for storage (SHA256)
    /// </summary>
    string HashToken(string token);
}
