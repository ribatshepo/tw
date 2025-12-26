using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using QRCoder;
using USP.Core.Models.DTOs.Authentication;
using USP.Core.Models.Entities;
using USP.Core.Services.Authentication;
using USP.Core.Services.Communication;
using USP.Infrastructure.Data;

namespace USP.Infrastructure.Services.Authentication;

/// <summary>
/// Passwordless authentication service using magic links, QR codes, and SMS links
/// </summary>
public class PasswordlessAuthService : IPasswordlessAuthService
{
    private readonly ApplicationDbContext _context;
    private readonly IJwtService _jwtService;
    private readonly ILogger<PasswordlessAuthService> _logger;
    private readonly IEmailService _emailService;
    private readonly ISmsService _smsService;
    private readonly IMemoryCache _cache;

    private const int MagicLinkExpirationMinutes = 15;
    private const int QrCodeExpirationMinutes = 5;
    private const int SmsLinkExpirationMinutes = 15;
    private const string QrCodeCachePrefix = "qrcode:auth:";

    public PasswordlessAuthService(
        ApplicationDbContext context,
        IJwtService jwtService,
        ILogger<PasswordlessAuthService> logger,
        IEmailService emailService,
        ISmsService smsService,
        IMemoryCache cache)
    {
        _context = context;
        _jwtService = jwtService;
        _logger = logger;
        _emailService = emailService;
        _smsService = smsService;
        _cache = cache;
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

    public async Task<QrCodeAuthResponse> GenerateQrCodeAsync(QrCodeAuthRequest request)
    {
        try
        {
            _logger.LogInformation("Generating QR code for authentication, identifier: {Identifier}", request.Identifier);

            // Find user by email or username
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == request.Identifier || u.UserName == request.Identifier);

            if (user == null)
            {
                // Don't reveal if user exists for security
                _logger.LogWarning("QR code requested for non-existent user {Identifier}", request.Identifier);
                throw new InvalidOperationException("Invalid user identifier");
            }

            // Generate QR code token
            var token = GenerateMagicLinkToken();

            // Create QR code authentication session
            var qrCodeAuth = new QrCodeAuth
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Token = token,
                SessionId = request.SessionId,
                IsScanned = false,
                IsApproved = false,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(QrCodeExpirationMinutes)
            };

            _context.Set<QrCodeAuth>().Add(qrCodeAuth);
            await _context.SaveChangesAsync();

            // Generate QR code image containing the token and session ID
            var qrPayload = $"usp://auth/qr?token={token}&session={request.SessionId}";
            var qrCodeDataUrl = GenerateQrCodeImage(qrPayload);

            // Also cache the QR code session for fast lookup
            var cacheKey = $"{QrCodeCachePrefix}{token}";
            _cache.Set(cacheKey, qrCodeAuth, TimeSpan.FromMinutes(QrCodeExpirationMinutes));

            _logger.LogInformation("QR code generated for user {UserId}, session {SessionId}", user.Id, request.SessionId);

            return new QrCodeAuthResponse
            {
                QrCodeDataUrl = qrCodeDataUrl,
                Token = token,
                ExpiresAt = qrCodeAuth.ExpiresAt,
                SessionId = request.SessionId
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating QR code");
            throw;
        }
    }

    public async Task<LoginResponse> VerifyQrCodeAsync(VerifyQrCodeRequest request)
    {
        try
        {
            _logger.LogInformation("Verifying QR code scan, token: {Token}", request.Token.Substring(0, 8) + "...");

            // Find QR code authentication session
            var qrCodeAuth = await _context.Set<QrCodeAuth>()
                .Include(q => q.User)
                .FirstOrDefaultAsync(q => q.Token == request.Token && q.SessionId == request.SessionId);

            if (qrCodeAuth == null)
            {
                throw new InvalidOperationException("Invalid or expired QR code");
            }

            // Check expiration
            if (qrCodeAuth.ExpiresAt < DateTime.UtcNow)
            {
                throw new InvalidOperationException("QR code has expired");
            }

            // Check if already used
            if (qrCodeAuth.IsApproved)
            {
                throw new InvalidOperationException("QR code has already been used");
            }

            // Mark as scanned and approved
            qrCodeAuth.IsScanned = true;
            qrCodeAuth.IsApproved = true;
            qrCodeAuth.ScannedAt = DateTime.UtcNow;
            qrCodeAuth.ApprovedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            // Generate JWT tokens
            var accessToken = _jwtService.GenerateAccessToken(qrCodeAuth.User, new List<string>());
            var refreshToken = GenerateRefreshToken();

            // Remove from cache
            var cacheKey = $"{QrCodeCachePrefix}{request.Token}";
            _cache.Remove(cacheKey);

            _logger.LogInformation("QR code verified successfully for user {UserId}", qrCodeAuth.UserId);

            return new LoginResponse
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresAt = DateTime.UtcNow.AddMinutes(60),
                User = new UserInfo
                {
                    Id = qrCodeAuth.User.Id,
                    Username = qrCodeAuth.User.UserName ?? string.Empty,
                    Email = qrCodeAuth.User.Email ?? string.Empty,
                    FirstName = qrCodeAuth.User.FirstName,
                    LastName = qrCodeAuth.User.LastName,
                    MfaEnabled = qrCodeAuth.User.MfaEnabled
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying QR code");
            throw;
        }
    }

    public async Task<SmsLinkAuthResponse> SendSmsLinkAsync(SmsLinkAuthRequest request)
    {
        try
        {
            _logger.LogInformation("Sending SMS authentication link to {PhoneNumber}", request.PhoneNumber);

            // Find user by verified phone number
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.VerifiedPhoneNumber == request.PhoneNumber && u.PhoneNumberVerified);

            if (user == null)
            {
                // Don't reveal if user exists for security
                _logger.LogWarning("SMS link requested for non-existent or unverified phone {PhoneNumber}", request.PhoneNumber);
                return new SmsLinkAuthResponse
                {
                    Success = true,
                    Message = "If an account exists with this phone number, an SMS link has been sent"
                };
            }

            // Generate SMS link token
            var token = GenerateMagicLinkToken();

            // Store SMS link in database
            var magicLink = new MagicLink
            {
                Id = Guid.NewGuid(),
                Token = token,
                UserId = user.Id,
                Email = user.Email ?? string.Empty,
                RedirectUrl = request.RedirectUrl,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(SmsLinkExpirationMinutes)
            };

            _context.Set<MagicLink>().Add(magicLink);
            await _context.SaveChangesAsync();

            // Generate SMS link URL
            var smsLinkUrl = $"{request.RedirectUrl ?? "https://localhost:8443"}/auth/sms-link/verify?token={token}";

            // Send SMS
            var smsMessage = $"Your login link (expires in {SmsLinkExpirationMinutes} minutes): {smsLinkUrl}";
            var sent = await _smsService.SendSmsAsync(request.PhoneNumber, smsMessage);

            if (!sent)
            {
                _logger.LogError("Failed to send SMS authentication link to {PhoneNumber}", request.PhoneNumber);
                return new SmsLinkAuthResponse
                {
                    Success = false,
                    Message = "Failed to send SMS link. Please try again later."
                };
            }

            _logger.LogInformation("SMS authentication link sent to user {UserId}", user.Id);

            return new SmsLinkAuthResponse
            {
                Success = true,
                Message = "Authentication link sent to your phone"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending SMS link");
            return new SmsLinkAuthResponse
            {
                Success = false,
                Message = "Failed to send SMS link"
            };
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

    private static string GenerateQrCodeImage(string payload)
    {
        try
        {
            using var qrGenerator = new QRCodeGenerator();
            using var qrCodeData = qrGenerator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new PngByteQRCode(qrCodeData);
            var qrCodeBytes = qrCode.GetGraphic(20);

            return $"data:image/png;base64,{Convert.ToBase64String(qrCodeBytes)}";
        }
        catch (Exception)
        {
            throw new InvalidOperationException("Failed to generate QR code image");
        }
    }

    #endregion
}
