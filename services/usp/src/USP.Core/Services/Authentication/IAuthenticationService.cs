using USP.Core.Models.DTOs.Authentication;

namespace USP.Core.Services.Authentication;

/// <summary>
/// Authentication service for user login, registration, and token management
/// </summary>
public interface IAuthenticationService
{
    /// <summary>
    /// Authenticate user with username and password
    /// </summary>
    Task<LoginResponse> LoginAsync(LoginRequest request, string ipAddress, string userAgent);

    /// <summary>
    /// Register new user
    /// </summary>
    Task<LoginResponse> RegisterAsync(RegisterRequest request, string ipAddress, string userAgent);

    /// <summary>
    /// Refresh access token using refresh token
    /// </summary>
    Task<LoginResponse> RefreshTokenAsync(RefreshTokenRequest request, string ipAddress, string userAgent);

    /// <summary>
    /// Logout user and revoke session
    /// </summary>
    Task LogoutAsync(Guid userId, string token);

    /// <summary>
    /// Verify MFA code
    /// </summary>
    Task<bool> VerifyMfaCodeAsync(Guid userId, string code);
}
