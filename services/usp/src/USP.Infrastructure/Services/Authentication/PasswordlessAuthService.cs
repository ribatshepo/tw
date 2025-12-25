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

/// <summary>
/// Risk-based authentication service for adaptive security
/// </summary>
public class RiskAssessmentService : IRiskAssessmentService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<RiskAssessmentService> _logger;

    public RiskAssessmentService(
        ApplicationDbContext context,
        ILogger<RiskAssessmentService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<RiskAssessmentResponse> AssessRiskAsync(RiskAssessmentRequest request)
    {
        try
        {
            _logger.LogInformation("Assessing risk for user {UserId} from IP {IpAddress}",
                request.UserId, request.IpAddress);

            var riskFactors = new List<string>();
            var riskScore = 0;

            // Check for new IP address
            var previousLogins = await _context.Sessions
                .Where(s => s.UserId == request.UserId)
                .OrderByDescending(s => s.CreatedAt)
                .Take(10)
                .ToListAsync();

            if (previousLogins.Any() && !previousLogins.Any(s => s.IpAddress?.ToString() == request.IpAddress))
            {
                riskFactors.Add("New IP address");
                riskScore += 20;
            }

            // Check for new device
            if (!string.IsNullOrEmpty(request.DeviceFingerprint))
            {
                var knownDevices = await _context.TrustedDevices
                    .Where(d => d.UserId == request.UserId && d.IsActive)
                    .ToListAsync();

                if (!knownDevices.Any(d => d.DeviceFingerprint == request.DeviceFingerprint))
                {
                    riskFactors.Add("New device");
                    riskScore += 15;
                }
            }

            // Check for unusual time
            var currentHour = DateTime.UtcNow.Hour;
            if (currentHour < 6 || currentHour > 23)
            {
                riskFactors.Add("Unusual login time");
                riskScore += 10;
            }

            // Check for rapid successive attempts
            var recentAttempts = await _context.AuditLogs
                .Where(al => al.UserId == request.UserId &&
                            al.Action == "login_attempt" &&
                            al.CreatedAt > DateTime.UtcNow.AddMinutes(-5))
                .CountAsync();

            if (recentAttempts > 3)
            {
                riskFactors.Add("Multiple login attempts");
                riskScore += 25;
            }

            // Check for known bad IP (simplified - production would use threat intelligence)
            if (request.IpAddress.StartsWith("192.0.2.") || request.IpAddress.StartsWith("198.51.100."))
            {
                riskFactors.Add("Suspicious IP range");
                riskScore += 40;
            }

            // Determine risk level
            var riskLevel = riskScore switch
            {
                >= 70 => "critical",
                >= 50 => "high",
                >= 30 => "medium",
                _ => "low"
            };

            var requireAdditionalVerification = riskScore >= 30;

            var recommendedActions = new List<string>();
            if (riskLevel == "critical")
            {
                recommendedActions.Add("Block login attempt");
                recommendedActions.Add("Send security alert to user");
            }
            else if (riskLevel == "high")
            {
                recommendedActions.Add("Require MFA");
                recommendedActions.Add("Send security notification");
            }
            else if (riskLevel == "medium")
            {
                recommendedActions.Add("Require MFA");
            }

            _logger.LogInformation("Risk assessment for user {UserId}: {RiskLevel} (score: {RiskScore})",
                request.UserId, riskLevel, riskScore);

            return new RiskAssessmentResponse
            {
                RiskLevel = riskLevel,
                RiskScore = riskScore,
                RiskFactors = riskFactors,
                RequireAdditionalVerification = requireAdditionalVerification,
                RecommendedActions = recommendedActions
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assessing risk");
            return new RiskAssessmentResponse
            {
                RiskLevel = "unknown",
                RiskScore = 50,
                RiskFactors = new List<string> { "Risk assessment error" },
                RequireAdditionalVerification = true,
                RecommendedActions = new List<string> { "Require MFA" }
            };
        }
    }

    public async Task RecordAssessmentAsync(Guid userId, RiskAssessmentResponse assessment, string action)
    {
        try
        {
            var riskAssessment = new RiskAssessment
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                IpAddress = string.Empty,
                UserAgent = string.Empty,
                RiskLevel = assessment.RiskLevel,
                RiskScore = assessment.RiskScore,
                RiskFactors = assessment.RiskFactors,
                Action = action,
                AssessedAt = DateTime.UtcNow
            };

            _context.Set<RiskAssessment>().Add(riskAssessment);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Risk assessment recorded for user {UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording risk assessment");
        }
    }
}
