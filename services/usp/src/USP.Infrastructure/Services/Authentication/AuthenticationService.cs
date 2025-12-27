using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using USP.Core.Domain.Entities.Identity;
using USP.Core.Domain.Enums;
using USP.Core.Interfaces.Services;
using USP.Core.Interfaces.Services.Authentication;

namespace USP.Infrastructure.Services.Authentication;

/// <summary>
/// Implements authentication operations including login, logout, registration, and password management.
/// </summary>
public class AuthenticationService : IAuthenticationService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITokenService _tokenService;
    private readonly ISessionService _sessionService;
    private readonly IEmailService _emailService;

    public AuthenticationService(
        UserManager<ApplicationUser> userManager,
        ITokenService tokenService,
        ISessionService _sessionService,
        IEmailService emailService)
    {
        _userManager = userManager;
        _tokenService = tokenService;
        this._sessionService = _sessionService;
        _emailService = emailService;
    }

    public async Task<ApplicationUser> RegisterAsync(
        string email,
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        // Check if user already exists
        var existingUserByEmail = await _userManager.FindByEmailAsync(email);
        if (existingUserByEmail != null)
        {
            throw new InvalidOperationException("User with this email already exists");
        }

        var existingUserByUsername = await _userManager.FindByNameAsync(username);
        if (existingUserByUsername != null)
        {
            throw new InvalidOperationException("User with this username already exists");
        }

        // Create new user
        var user = new ApplicationUser
        {
            UserName = username,
            Email = email,
            EmailConfirmed = false,
            Status = UserStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            PasswordChangedAt = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to create user: {errors}");
        }

        var emailToken = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        await _emailService.SendEmailVerificationAsync(user.Email!, emailToken, cancellationToken);

        return user;
    }

    public async Task<AuthenticationResult> LoginAsync(
        string emailOrUsername,
        string password,
        string ipAddress,
        string userAgent,
        string? deviceFingerprint = null,
        CancellationToken cancellationToken = default)
    {
        // Find user by email or username
        var user = await _userManager.FindByEmailAsync(emailOrUsername)
                   ?? await _userManager.FindByNameAsync(emailOrUsername)
                   ?? throw new UnauthorizedAccessException("Invalid credentials");

        // Check if user can authenticate
        if (!user.CanAuthenticate())
        {
            throw new UnauthorizedAccessException($"Account is {user.Status}");
        }

        // Verify password
        var passwordValid = await _userManager.CheckPasswordAsync(user, password);
        if (!passwordValid)
        {
            // Increment failed login attempts
            user.FailedLoginAttempts++;
            user.LastFailedLogin = DateTime.UtcNow;

            // Lock account after 5 failed attempts
            if (user.FailedLoginAttempts >= 5)
            {
                user.LockedUntil = DateTime.UtcNow.AddMinutes(15);
                await _userManager.UpdateAsync(user);
                throw new UnauthorizedAccessException("Account locked due to too many failed attempts. Try again in 15 minutes.");
            }

            await _userManager.UpdateAsync(user);
            throw new UnauthorizedAccessException("Invalid credentials");
        }

        // Reset failed login attempts on successful login
        user.FailedLoginAttempts = 0;
        user.LastFailedLogin = null;
        user.LastLoginAt = DateTime.UtcNow;
        user.LastLoginIp = ipAddress;
        await _userManager.UpdateAsync(user);

        // Get user roles
        var roles = await _userManager.GetRolesAsync(user);

        // Generate tokens
        var accessToken = await _tokenService.GenerateAccessTokenAsync(
            user.Id,
            user.Email!,
            roles);

        var refreshToken = _tokenService.GenerateRefreshToken();
        var expiresIn = _tokenService.GetAccessTokenExpirationSeconds();

        // Create session
        await _sessionService.CreateSessionAsync(
            user.Id,
            refreshToken,
            ipAddress,
            userAgent,
            deviceFingerprint,
            cancellationToken);

        return new AuthenticationResult
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresIn = expiresIn,
            TokenType = "Bearer",
            User = user
        };
    }

    public async Task<AuthenticationResult> RefreshTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        // Retrieve session by refresh token
        var session = await _sessionService.GetSessionByRefreshTokenAsync(refreshToken, cancellationToken);
        if (session == null || !session.IsValid())
        {
            throw new UnauthorizedAccessException("Invalid or expired refresh token");
        }

        // Get user
        var user = await _userManager.FindByIdAsync(session.UserId);
        if (user == null || !user.CanAuthenticate())
        {
            throw new UnauthorizedAccessException("User not found or cannot authenticate");
        }

        // Get user roles
        var roles = await _userManager.GetRolesAsync(user);

        // Generate new tokens
        var newAccessToken = await _tokenService.GenerateAccessTokenAsync(
            user.Id,
            user.Email!,
            roles);

        var newRefreshToken = _tokenService.GenerateRefreshToken();
        var expiresIn = _tokenService.GetAccessTokenExpirationSeconds();

        // Revoke old session and create new one
        await _sessionService.RevokeSessionAsync(session.Id, cancellationToken);
        await _sessionService.CreateSessionAsync(
            user.Id,
            newRefreshToken,
            session.IpAddress,
            session.UserAgent ?? string.Empty,
            session.DeviceFingerprint,
            cancellationToken);

        return new AuthenticationResult
        {
            AccessToken = newAccessToken,
            RefreshToken = newRefreshToken,
            ExpiresIn = expiresIn,
            TokenType = "Bearer",
            User = user
        };
    }

    public async Task LogoutAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        await _sessionService.RevokeSessionAsync(sessionId, cancellationToken);
    }

    public async Task LogoutAllAsync(string userId, CancellationToken cancellationToken = default)
    {
        await _sessionService.RevokeAllSessionsAsync(userId, cancellationToken);
    }

    public async Task ChangePasswordAsync(
        string userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId)
                   ?? throw new InvalidOperationException("User not found");

        var result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to change password: {errors}");
        }

        // Update password changed timestamp
        user.PasswordChangedAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        // Revoke all sessions to force re-login
        await _sessionService.RevokeAllSessionsAsync(userId, cancellationToken);
    }

    public async Task ForgotPasswordAsync(string email, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null)
        {
            // Don't reveal that user doesn't exist
            return;
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        await _emailService.SendPasswordResetAsync(email, token, cancellationToken);
    }

    public async Task ResetPasswordAsync(
        string email,
        string token,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        // Look up user by email directly
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null)
        {
            // Don't reveal if user exists (security best practice)
            throw new InvalidOperationException("Invalid or expired password reset token");
        }

        // Verify the token for this specific user
        var result = await _userManager.ResetPasswordAsync(user, token, newPassword);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to reset password: {errors}");
        }

        // Update password changed timestamp
        user.PasswordChangedAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        // Revoke all sessions for security
        await _sessionService.RevokeAllSessionsAsync(user.Id, cancellationToken);
    }

    public async Task<ApplicationUser?> GetCurrentUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _userManager.Users
            .Include(u => u.MFADevices)
            .Include(u => u.TrustedDevices)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
    }
}
