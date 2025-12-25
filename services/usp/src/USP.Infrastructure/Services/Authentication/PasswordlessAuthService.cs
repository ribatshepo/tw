using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using USP.Core.Models.DTOs.Authentication;
using USP.Core.Models.Entities;
using USP.Core.Services.Authentication;
using USP.Core.Services.Communication;
using USP.Infrastructure.Data;

namespace USP.Infrastructure.Services.Authentication;

/// <summary>
/// Passwordless authentication service using magic links
/// </summary>
public class PasswordlessAuthService : IPasswordlessAuthService
{
    private readonly ApplicationDbContext _context;
    private readonly IJwtService _jwtService;
    private readonly ILogger<PasswordlessAuthService> _logger;
    private readonly IEmailService _emailService;

    public PasswordlessAuthService(
        ApplicationDbContext context,
        IJwtService jwtService,
        ILogger<PasswordlessAuthService> logger,
        IEmailService emailService)
    {
        _context = context;
        _jwtService = jwtService;
        _logger = logger;
        _emailService = emailService;
    }

    public async Task<PasswordlessAuthenticationResponse> SendMagicLinkAsync(PasswordlessAuthenticationRequest request)
    {
        try
        {
            _logger.LogInformation("Sending magic link to {Email}", request.Email);

            // Find user by email
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == request.Email);

            if (user == null)
            {
                // Don't reveal if user exists or not for security
                _logger.LogWarning("Magic link requested for non-existent email {Email}", request.Email);
                return new PasswordlessAuthenticationResponse
                {
                    Success = true,
                    Message = "If an account exists with this email, a magic link has been sent"
                };
            }

            // Generate magic link token
            var token = GenerateMagicLinkToken();

            // Store magic link
            var magicLink = new MagicLink
            {
                Id = Guid.NewGuid(),
                Token = token,
                UserId = user.Id,
                Email = user.Email ?? string.Empty,
                RedirectUrl = request.RedirectUrl,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(15) // 15 minute expiry
            };

            _context.Set<MagicLink>().Add(magicLink);
            await _context.SaveChangesAsync();

            var magicLinkUrl = $"{request.RedirectUrl ?? "https://localhost:8443"}/auth/magic-link/verify?token={token}";

            var emailSent = await _emailService.SendMagicLinkAsync(
                user.Email ?? string.Empty,
                magicLinkUrl,
                user.UserName ?? user.Email ?? "User"
            );

            if (!emailSent)
            {
                _logger.LogError("Failed to send magic link email to {Email}", user.Email);
                return new PasswordlessAuthenticationResponse
                {
                    Success = false,
                    Message = "Failed to send magic link. Please try again later."
                };
            }

            return new PasswordlessAuthenticationResponse
            {
                Success = true,
                Message = "Magic link sent to your email"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending magic link");
            return new PasswordlessAuthenticationResponse
            {
                Success = false,
                Message = "Failed to send magic link"
            };
        }
    }

    public async Task<LoginResponse> VerifyMagicLinkAsync(VerifyMagicLinkRequest request)
    {
        try
        {
            _logger.LogInformation("Verifying magic link token");

            // Find magic link
            var magicLink = await _context.Set<MagicLink>()
                .Include(ml => ml.User)
                .FirstOrDefaultAsync(ml => ml.Token == request.Token && !ml.IsUsed);

            if (magicLink == null)
            {
                throw new InvalidOperationException("Invalid or expired magic link");
            }

            // Check expiration
            if (magicLink.ExpiresAt < DateTime.UtcNow)
            {
                throw new InvalidOperationException("Magic link has expired");
            }

            // Mark as used
            magicLink.IsUsed = true;
            magicLink.UsedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            // Generate JWT tokens using the existing JWT service
            var accessToken = _jwtService.GenerateAccessToken(magicLink.User, new List<string>());
            var refreshToken = GenerateRefreshToken();

            _logger.LogInformation("Magic link verified for user {UserId}", magicLink.UserId);

            return new LoginResponse
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresAt = DateTime.UtcNow.AddMinutes(15)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying magic link");
            throw;
        }
    }

    #region Private Helper Methods

    private static string GenerateMagicLinkToken()
    {
        var bytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(bytes);
        }
        return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").Replace("=", "");
    }

    private static string GenerateRefreshToken()
    {
        var bytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(bytes);
        }
        return Convert.ToBase64String(bytes);
    }

    #endregion
}
