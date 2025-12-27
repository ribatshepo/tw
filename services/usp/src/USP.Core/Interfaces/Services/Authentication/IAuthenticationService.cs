using USP.Core.Domain.Entities.Identity;

namespace USP.Core.Interfaces.Services.Authentication;

/// <summary>
/// Provides authentication operations including login, logout, registration, and password management.
/// </summary>
public interface IAuthenticationService
{
    /// <summary>
    /// Registers a new user and sends a verification email.
    /// </summary>
    /// <param name="email">User's email address</param>
    /// <param name="username">User's username</param>
    /// <param name="password">User's password</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The created user</returns>
    Task<ApplicationUser> RegisterAsync(
        string email,
        string username,
        string password,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Authenticates a user with email/username and password.
    /// Creates a session and generates access/refresh tokens.
    /// </summary>
    /// <param name="emailOrUsername">Email or username</param>
    /// <param name="password">Password</param>
    /// <param name="ipAddress">Client IP address</param>
    /// <param name="userAgent">Client user agent</param>
    /// <param name="deviceFingerprint">Optional device fingerprint</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Authentication result with tokens</returns>
    Task<AuthenticationResult> LoginAsync(
        string emailOrUsername,
        string password,
        string ipAddress,
        string userAgent,
        string? deviceFingerprint = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Refreshes an access token using a valid refresh token.
    /// </summary>
    /// <param name="refreshToken">The refresh token</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>New access token and refresh token</returns>
    Task<AuthenticationResult> RefreshTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Logs out a user by revoking their session.
    /// </summary>
    /// <param name="sessionId">The session ID to revoke</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task LogoutAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Logs out a user from all sessions.
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task LogoutAllAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Changes a user's password.
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="currentPassword">The current password</param>
    /// <param name="newPassword">The new password</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ChangePasswordAsync(
        string userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a password reset token and sends it via email.
    /// </summary>
    /// <param name="email">The user's email address</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ForgotPasswordAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets a user's password using a reset token.
    /// </summary>
    /// <param name="email">The user's email address</param>
    /// <param name="token">The password reset token</param>
    /// <param name="newPassword">The new password</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ResetPasswordAsync(
        string email,
        string token,
        string newPassword,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current user's profile.
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The user entity</returns>
    Task<ApplicationUser?> GetCurrentUserAsync(string userId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of authentication operations.
/// </summary>
public class AuthenticationResult
{
    public required string AccessToken { get; set; }
    public required string RefreshToken { get; set; }
    public required int ExpiresIn { get; set; }
    public required string TokenType { get; set; } = "Bearer";
    public required ApplicationUser User { get; set; }
}
