using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OtpNet;
using QRCoder;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using USP.Core.Models.DTOs.Mfa;
using USP.Core.Models.Entities;
using USP.Core.Services.Communication;
using USP.Core.Services.Mfa;
using USP.Infrastructure.Data;
using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;

namespace USP.Infrastructure.Services.Mfa;

/// <summary>
/// Service for Multi-Factor Authentication operations
/// </summary>
public class MfaService : IMfaService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration _configuration;
    private readonly ILogger<MfaService> _logger;
    private readonly ISmsService _smsService;
    private readonly IMemoryCache _cache;
    private readonly IHttpClientFactory _httpClientFactory;

    private const int BackupCodeCount = 10;
    private const int BackupCodeLength = 8;
    private const string PhoneVerificationCodeCachePrefix = "mfa:phone:verify:";
    private const string SmsOtpCodeCachePrefix = "mfa:sms:otp:";
    private const string YubiKeyOtpCachePrefix = "mfa:yubikey:otp:";
    private const int VerificationCodeExpirationMinutes = 10;
    private const int SmsOtpExpirationMinutes = 5;
    private const int YubiKeyOtpCacheExpirationSeconds = 60;

    public MfaService(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration,
        ILogger<MfaService> logger,
        ISmsService smsService,
        IMemoryCache cache,
        IHttpClientFactory httpClientFactory)
    {
        _context = context;
        _userManager = userManager;
        _configuration = configuration;
        _logger = logger;
        _smsService = smsService;
        _cache = cache;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<EnrollTotpResponse> EnrollTotpAsync(Guid userId, string deviceName)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            throw new InvalidOperationException("User not found");
        }

        // Generate secret key for TOTP
        var secretKey = KeyGeneration.GenerateRandomKey(20);
        var base32Secret = Base32Encoding.ToString(secretKey);

        // Store secret temporarily (will be confirmed after verification)
        user.MfaSecret = base32Secret;
        await _userManager.UpdateAsync(user);

        // Generate QR code
        var issuer = _configuration["Jwt:Issuer"] ?? "USP";
        var accountName = user.Email ?? user.UserName ?? userId.ToString();
        var otpAuthUrl = $"otpauth://totp/{Uri.EscapeDataString(issuer)}:{Uri.EscapeDataString(accountName)}?secret={base32Secret}&issuer={Uri.EscapeDataString(issuer)}";

        var qrCodeDataUrl = GenerateQrCode(otpAuthUrl);

        _logger.LogInformation("TOTP enrollment initiated for user {UserId}", userId);

        return new EnrollTotpResponse
        {
            Secret = base32Secret,
            QrCodeDataUrl = qrCodeDataUrl,
            ManualEntryKey = FormatSecretForManualEntry(base32Secret),
            Issuer = issuer,
            AccountName = accountName
        };
    }

    public async Task<VerifyTotpResponse> VerifyAndCompleteTotpEnrollmentAsync(Guid userId, string code)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            throw new InvalidOperationException("User not found");
        }

        if (string.IsNullOrEmpty(user.MfaSecret))
        {
            throw new InvalidOperationException("No pending MFA enrollment found");
        }

        // Verify the code
        var isValid = VerifyTotpCode(user.MfaSecret, code);
        if (!isValid)
        {
            throw new InvalidOperationException("Invalid verification code");
        }

        // Enable MFA for user
        user.MfaEnabled = true;
        await _userManager.UpdateAsync(user);

        // Create MFA device record
        var device = new MfaDevice
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            DeviceType = "TOTP",
            DeviceName = "Authenticator App",
            IsActive = true,
            IsPrimary = true,
            RegisteredAt = DateTime.UtcNow
        };

        _context.MfaDevices.Add(device);
        await _context.SaveChangesAsync();

        // Generate backup codes
        var backupCodes = await GenerateBackupCodesInternalAsync(userId);

        _logger.LogInformation("TOTP enrollment completed for user {UserId}", userId);

        return new VerifyTotpResponse
        {
            IsValid = true,
            MfaEnabled = true,
            BackupCodes = backupCodes
        };
    }

    public async Task<bool> VerifyTotpCodeAsync(Guid userId, string code)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null || string.IsNullOrEmpty(user.MfaSecret))
        {
            return false;
        }

        var isValid = VerifyTotpCode(user.MfaSecret, code);

        if (isValid)
        {
            // Update last used timestamp for TOTP device
            var device = await _context.MfaDevices
                .FirstOrDefaultAsync(d => d.UserId == userId && d.DeviceType == "TOTP" && d.IsActive);

            if (device != null)
            {
                device.LastUsedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }

            _logger.LogInformation("TOTP code verified successfully for user {UserId}", userId);
        }

        return isValid;
    }

    public async Task<GenerateBackupCodesResponse> GenerateBackupCodesAsync(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            throw new InvalidOperationException("User not found");
        }

        if (!user.MfaEnabled)
        {
            throw new InvalidOperationException("MFA is not enabled for this user");
        }

        // Invalidate existing backup codes
        var existingCodes = await _context.MfaBackupCodes
            .Where(c => c.UserId == userId && !c.IsUsed)
            .ToListAsync();

        _context.MfaBackupCodes.RemoveRange(existingCodes);

        // Generate new backup codes
        var codes = await GenerateBackupCodesInternalAsync(userId);

        _logger.LogInformation("Generated {Count} backup codes for user {UserId}", codes.Count, userId);

        return new GenerateBackupCodesResponse
        {
            BackupCodes = codes,
            TotalCodes = codes.Count,
            GeneratedAt = DateTime.UtcNow
        };
    }

    public async Task<bool> VerifyBackupCodeAsync(Guid userId, string code)
    {
        var codeHash = HashBackupCode(code);

        var backupCode = await _context.MfaBackupCodes
            .FirstOrDefaultAsync(c => c.UserId == userId && c.CodeHash == codeHash && !c.IsUsed);

        if (backupCode == null)
        {
            return false;
        }

        // Mark code as used
        backupCode.IsUsed = true;
        backupCode.UsedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Backup code verified and consumed for user {UserId}", userId);

        return true;
    }

    public async Task<IEnumerable<MfaDeviceDto>> GetUserMfaDevicesAsync(Guid userId)
    {
        var devices = await _context.MfaDevices
            .Where(d => d.UserId == userId && d.IsActive)
            .OrderByDescending(d => d.IsPrimary)
            .ThenByDescending(d => d.RegisteredAt)
            .ToListAsync();

        return devices.Select(d => new MfaDeviceDto
        {
            Id = d.Id,
            DeviceType = d.DeviceType,
            DeviceName = d.DeviceName,
            IsActive = d.IsActive,
            IsPrimary = d.IsPrimary,
            RegisteredAt = d.RegisteredAt,
            LastUsedAt = d.LastUsedAt
        });
    }

    public async Task<bool> DisableMfaAsync(Guid userId, string password)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return false;
        }

        // Verify password
        var isPasswordValid = await _userManager.CheckPasswordAsync(user, password);
        if (!isPasswordValid)
        {
            throw new InvalidOperationException("Invalid password");
        }

        // Disable MFA
        user.MfaEnabled = false;
        user.MfaSecret = null;
        await _userManager.UpdateAsync(user);

        // Deactivate all MFA devices
        var devices = await _context.MfaDevices
            .Where(d => d.UserId == userId)
            .ToListAsync();

        foreach (var device in devices)
        {
            device.IsActive = false;
        }

        // Remove all backup codes
        var backupCodes = await _context.MfaBackupCodes
            .Where(c => c.UserId == userId)
            .ToListAsync();

        _context.MfaBackupCodes.RemoveRange(backupCodes);

        await _context.SaveChangesAsync();

        _logger.LogInformation("MFA disabled for user {UserId}", userId);

        return true;
    }

    public async Task<bool> RemoveMfaDeviceAsync(Guid userId, Guid deviceId)
    {
        var device = await _context.MfaDevices
            .FirstOrDefaultAsync(d => d.Id == deviceId && d.UserId == userId);

        if (device == null)
        {
            return false;
        }

        device.IsActive = false;
        await _context.SaveChangesAsync();

        _logger.LogInformation("MFA device {DeviceId} removed for user {UserId}", deviceId, userId);

        return true;
    }

    public async Task<bool> SetPrimaryMfaDeviceAsync(Guid userId, Guid deviceId)
    {
        var device = await _context.MfaDevices
            .FirstOrDefaultAsync(d => d.Id == deviceId && d.UserId == userId && d.IsActive);

        if (device == null)
        {
            return false;
        }

        // Remove primary flag from other devices
        var otherDevices = await _context.MfaDevices
            .Where(d => d.UserId == userId && d.Id != deviceId && d.IsPrimary)
            .ToListAsync();

        foreach (var otherDevice in otherDevices)
        {
            otherDevice.IsPrimary = false;
        }

        device.IsPrimary = true;
        await _context.SaveChangesAsync();

        _logger.LogInformation("MFA device {DeviceId} set as primary for user {UserId}", deviceId, userId);

        return true;
    }

    public async Task<bool> SendPhoneVerificationAsync(Guid userId, string phoneNumber)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            throw new InvalidOperationException("User not found");
        }

        // Generate 6-digit verification code
        var code = GenerateSixDigitCode();

        // Store code in cache with expiration
        var cacheKey = $"{PhoneVerificationCodeCachePrefix}{userId}";
        _cache.Set(cacheKey, new { Code = code, PhoneNumber = phoneNumber },
            TimeSpan.FromMinutes(VerificationCodeExpirationMinutes));

        // Send SMS
        var sent = await _smsService.SendOtpSmsAsync(phoneNumber, code, VerificationCodeExpirationMinutes);

        if (sent)
        {
            _logger.LogInformation("Phone verification code sent to user {UserId}", userId);
        }

        return sent;
    }

    public async Task<bool> VerifyPhoneNumberAsync(Guid userId, string code)
    {
        var cacheKey = $"{PhoneVerificationCodeCachePrefix}{userId}";

        if (!_cache.TryGetValue<dynamic>(cacheKey, out var cachedData))
        {
            _logger.LogWarning("No pending phone verification found for user {UserId}", userId);
            return false;
        }

        if (cachedData.Code != code)
        {
            _logger.LogWarning("Invalid phone verification code for user {UserId}", userId);
            return false;
        }

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return false;
        }

        // Mark phone as verified
        user.VerifiedPhoneNumber = cachedData.PhoneNumber;
        user.PhoneNumberVerified = true;
        await _userManager.UpdateAsync(user);

        // Remove from cache
        _cache.Remove(cacheKey);

        _logger.LogInformation("Phone number verified for user {UserId}", userId);

        return true;
    }

    public async Task<bool> EnrollSmsMfaAsync(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            throw new InvalidOperationException("User not found");
        }

        if (!user.PhoneNumberVerified || string.IsNullOrEmpty(user.VerifiedPhoneNumber))
        {
            throw new InvalidOperationException("Phone number must be verified before enrolling SMS MFA");
        }

        // Enable MFA if not already enabled
        if (!user.MfaEnabled)
        {
            user.MfaEnabled = true;
            await _userManager.UpdateAsync(user);
        }

        // Create MFA device record
        var existingDevice = await _context.MfaDevices
            .FirstOrDefaultAsync(d => d.UserId == userId && d.DeviceType == "SMS" && d.IsActive);

        if (existingDevice != null)
        {
            _logger.LogInformation("SMS MFA device already exists for user {UserId}", userId);
            return true;
        }

        var device = new MfaDevice
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            DeviceType = "SMS",
            DeviceName = $"SMS ({MaskPhoneNumber(user.VerifiedPhoneNumber)})",
            IsActive = true,
            IsPrimary = false,
            RegisteredAt = DateTime.UtcNow
        };

        _context.MfaDevices.Add(device);
        await _context.SaveChangesAsync();

        _logger.LogInformation("SMS MFA enrolled for user {UserId}", userId);

        return true;
    }

    public async Task<bool> SendSmsOtpAsync(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null || string.IsNullOrEmpty(user.VerifiedPhoneNumber))
        {
            return false;
        }

        // Check if SMS MFA device exists and is active
        var smsDevice = await _context.MfaDevices
            .FirstOrDefaultAsync(d => d.UserId == userId && d.DeviceType == "SMS" && d.IsActive);

        if (smsDevice == null)
        {
            _logger.LogWarning("No active SMS MFA device found for user {UserId}", userId);
            return false;
        }

        // Generate 6-digit OTP code
        var code = GenerateSixDigitCode();

        // Store code in cache with expiration
        var cacheKey = $"{SmsOtpCodeCachePrefix}{userId}";
        _cache.Set(cacheKey, code, TimeSpan.FromMinutes(SmsOtpExpirationMinutes));

        // Send SMS
        var sent = await _smsService.SendOtpSmsAsync(user.VerifiedPhoneNumber, code, SmsOtpExpirationMinutes);

        if (sent)
        {
            _logger.LogInformation("SMS OTP code sent to user {UserId}", userId);

            // Update device last used timestamp
            smsDevice.LastUsedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        return sent;
    }

    public async Task<bool> VerifySmsOtpAsync(Guid userId, string code)
    {
        var cacheKey = $"{SmsOtpCodeCachePrefix}{userId}";

        if (!_cache.TryGetValue<string>(cacheKey, out var cachedCode))
        {
            _logger.LogWarning("No pending SMS OTP found for user {UserId}", userId);
            return false;
        }

        if (cachedCode != code)
        {
            _logger.LogWarning("Invalid SMS OTP code for user {UserId}", userId);
            return false;
        }

        // Remove from cache after successful verification
        _cache.Remove(cacheKey);

        _logger.LogInformation("SMS OTP verified successfully for user {UserId}", userId);

        return true;
    }

    public async Task<bool> SendVoiceOtpAsync(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null || string.IsNullOrEmpty(user.VerifiedPhoneNumber))
        {
            return false;
        }

        // Check if SMS MFA device exists and is active
        var smsDevice = await _context.MfaDevices
            .FirstOrDefaultAsync(d => d.UserId == userId && d.DeviceType == "SMS" && d.IsActive);

        if (smsDevice == null)
        {
            _logger.LogWarning("No active SMS MFA device found for user {UserId}", userId);
            return false;
        }

        // Generate 6-digit OTP code
        var code = GenerateSixDigitCode();

        // Store code in cache with expiration (reuse SMS OTP cache key)
        var cacheKey = $"{SmsOtpCodeCachePrefix}{userId}";
        _cache.Set(cacheKey, code, TimeSpan.FromMinutes(SmsOtpExpirationMinutes));

        // Send voice call
        var sent = await _smsService.SendOtpVoiceAsync(user.VerifiedPhoneNumber, code);

        if (sent)
        {
            _logger.LogInformation("Voice OTP code sent to user {UserId}", userId);

            // Update device last used timestamp
            smsDevice.LastUsedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        return sent;
    }

    public async Task<bool> SendPushNotificationAsync(Guid userId, string message, string actionType = "approve")
    {
        // Get user's push notification devices
        var pushDevices = await _context.MfaDevices
            .Where(d => d.UserId == userId &&
                       d.DeviceType == "Push" &&
                       d.IsActive &&
                       !string.IsNullOrEmpty(d.PushToken))
            .ToListAsync();

        if (!pushDevices.Any())
        {
            _logger.LogWarning("No active push notification devices found for user {UserId}", userId);
            return false;
        }

        // Store pending approval in cache
        var cacheKey = $"mfa:push:approval:{userId}";
        var cacheData = new
        {
            UserId = userId,
            Message = message,
            ActionType = actionType,
            SentAt = DateTime.UtcNow
        };
        _cache.Set(cacheKey, cacheData, TimeSpan.FromMinutes(5));

        // Send notification to all registered devices
        var successCount = 0;
        foreach (var device in pushDevices)
        {
            try
            {
                bool sent = false;

                if (device.DevicePlatform == "Android")
                {
                    sent = await SendFcmNotificationAsync(device.PushToken, message, actionType, userId);
                }
                else if (device.DevicePlatform == "iOS")
                {
                    sent = await SendApnsNotificationAsync(device.PushToken, message, actionType, userId);
                }
                else
                {
                    _logger.LogWarning("Unsupported device platform: {Platform}", device.DevicePlatform);
                    continue;
                }

                if (sent)
                {
                    successCount++;
                    device.LastUsedAt = DateTime.UtcNow;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to send push notification to device {DeviceId} for user {UserId}",
                    device.Id, userId);
            }
        }

        await _context.SaveChangesAsync();

        if (successCount > 0)
        {
            _logger.LogInformation(
                "Push notifications sent to {Count}/{Total} devices for user {UserId}",
                successCount, pushDevices.Count, userId);
            return true;
        }

        _logger.LogWarning("Failed to send push notifications to any device for user {UserId}", userId);
        return false;
    }

    /// <summary>
    /// Sends push notification via Firebase Cloud Messaging (FCM) for Android
    /// </summary>
    private async Task<bool> SendFcmNotificationAsync(string deviceToken, string message, string actionType, Guid userId)
    {
        try
        {
            // Check configuration
            var serviceAccountKeyPath = _configuration["MfaSettings:FcmServiceAccountKeyPath"];
            if (string.IsNullOrWhiteSpace(serviceAccountKeyPath))
            {
                _logger.LogError("FCM service account key path not configured");
                throw new InvalidOperationException(
                    "FCM is not configured. Set MfaSettings:FcmServiceAccountKeyPath in appsettings.json");
            }

            // Initialize Firebase if not already initialized
            if (FirebaseApp.DefaultInstance == null)
            {
                if (!File.Exists(serviceAccountKeyPath))
                {
                    _logger.LogError("FCM service account key file not found: {Path}", serviceAccountKeyPath);
                    throw new FileNotFoundException($"FCM service account key file not found: {serviceAccountKeyPath}");
                }

                FirebaseApp.Create(new AppOptions
                {
                    Credential = GoogleCredential.FromFile(serviceAccountKeyPath)
                });

                _logger.LogInformation("Firebase Admin SDK initialized");
            }

            // Create FCM message
            var fcmMessage = new Message
            {
                Token = deviceToken,
                Notification = new Notification
                {
                    Title = "MFA Verification Required",
                    Body = message
                },
                Data = new Dictionary<string, string>
                {
                    { "action_type", actionType },
                    { "user_id", userId.ToString() },
                    { "timestamp", DateTime.UtcNow.ToString("o") }
                },
                Android = new AndroidConfig
                {
                    Priority = Priority.High,
                    Notification = new AndroidNotification
                    {
                        ClickAction = "MFA_APPROVAL_ACTION",
                        Sound = "default"
                    }
                }
            };

            // Send message
            var response = await FirebaseMessaging.DefaultInstance.SendAsync(fcmMessage);

            _logger.LogInformation("FCM notification sent successfully. Message ID: {MessageId}", response);
            return true;
        }
        catch (FirebaseMessagingException ex)
        {
            _logger.LogError(ex, "FCM notification failed: {ErrorCode}", ex.MessagingErrorCode);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send FCM notification");
            return false;
        }
    }

    /// <summary>
    /// Sends push notification via Apple Push Notification Service (APNS) for iOS
    /// </summary>
    private async Task<bool> SendApnsNotificationAsync(string deviceToken, string message, string actionType, Guid userId)
    {
        try
        {
            // Check configuration
            var keyPath = _configuration["MfaSettings:ApnsKeyPath"];
            var keyId = _configuration["MfaSettings:ApnsKeyId"];
            var teamId = _configuration["MfaSettings:ApnsTeamId"];
            var bundleId = _configuration["MfaSettings:ApnsBundleId"];
            var useSandbox = _configuration.GetValue<bool>("MfaSettings:ApnsUseSandbox", true);

            if (string.IsNullOrWhiteSpace(keyPath) || string.IsNullOrWhiteSpace(keyId) ||
                string.IsNullOrWhiteSpace(teamId) || string.IsNullOrWhiteSpace(bundleId))
            {
                _logger.LogError("APNS configuration incomplete");
                throw new InvalidOperationException(
                    "APNS is not configured. Set MfaSettings:ApnsKeyPath, ApnsKeyId, ApnsTeamId, and ApnsBundleId in appsettings.json");
            }

            // Use Firebase Admin SDK for APNS (simpler than implementing HTTP/2 directly)
            // Firebase Admin SDK supports both FCM and APNS
            if (FirebaseApp.DefaultInstance != null)
            {
                var apnsMessage = new Message
                {
                    Token = deviceToken,
                    Notification = new Notification
                    {
                        Title = "MFA Verification Required",
                        Body = message
                    },
                    Data = new Dictionary<string, string>
                    {
                        { "action_type", actionType },
                        { "user_id", userId.ToString() },
                        { "timestamp", DateTime.UtcNow.ToString("o") }
                    },
                    Apns = new ApnsConfig
                    {
                        Aps = new Aps
                        {
                            Alert = new ApsAlert
                            {
                                Title = "MFA Verification Required",
                                Body = message
                            },
                            Badge = 1,
                            Sound = "default",
                            ContentAvailable = true
                        }
                    }
                };

                var response = await FirebaseMessaging.DefaultInstance.SendAsync(apnsMessage);
                _logger.LogInformation("APNS notification sent successfully via FCM. Message ID: {MessageId}", response);
                return true;
            }

            _logger.LogWarning("Firebase not initialized, skipping APNS notification");
            return false;
        }
        catch (FirebaseMessagingException ex)
        {
            _logger.LogError(ex, "APNS notification failed: {ErrorCode}", ex.MessagingErrorCode);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send APNS notification");
            return false;
        }
    }

    public async Task<bool> VerifyPushApprovalAsync(Guid userId, bool approved)
    {
        var cacheKey = $"mfa:push:approval:{userId}";

        if (!_cache.TryGetValue<dynamic>(cacheKey, out var cachedData))
        {
            _logger.LogWarning("No pending push approval found for user {UserId}", userId);
            return false;
        }

        if (!approved)
        {
            // User denied the push notification
            _cache.Remove(cacheKey);
            _logger.LogWarning("Push notification denied by user {UserId}", userId);
            return false;
        }

        // Remove from cache after successful approval
        _cache.Remove(cacheKey);

        _logger.LogInformation("Push notification approved by user {UserId}", userId);

        return true;
    }

    public async Task<bool> EnrollHardwareTokenAsync(Guid userId, string tokenSerial, string tokenType = "YubiKey")
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            throw new InvalidOperationException("User not found");
        }

        // Check if token already enrolled
        var existingDevice = await _context.MfaDevices
            .FirstOrDefaultAsync(d => d.UserId == userId &&
                                     d.DeviceType == "HardwareToken" &&
                                     d.DeviceName.Contains(tokenSerial) &&
                                     d.IsActive);

        if (existingDevice != null)
        {
            _logger.LogInformation("Hardware token already enrolled for user {UserId}", userId);
            return true;
        }

        // Enable MFA if not already enabled
        if (!user.MfaEnabled)
        {
            user.MfaEnabled = true;
            await _userManager.UpdateAsync(user);
        }

        // Create MFA device record
        var device = new MfaDevice
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            DeviceType = "HardwareToken",
            DeviceName = $"{tokenType} ({tokenSerial})",
            IsActive = true,
            IsPrimary = false,
            RegisteredAt = DateTime.UtcNow
        };

        _context.MfaDevices.Add(device);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Hardware token enrolled for user {UserId}, serial: {Serial}", userId, tokenSerial);

        return true;
    }

    public async Task<bool> VerifyHardwareTokenAsync(Guid userId, string otp)
    {
        // Validate configuration
        var clientId = _configuration["MfaSettings:YubicoClientId"];
        var secretKey = _configuration["MfaSettings:YubicoSecretKey"];

        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(secretKey))
        {
            _logger.LogError("YubiKey OTP validation failed: YubicoClientId or YubicoSecretKey not configured");
            throw new InvalidOperationException(
                "YubiKey OTP validation is not configured. " +
                "Set MfaSettings:YubicoClientId and MfaSettings:YubicoSecretKey in appsettings.json. " +
                "Get credentials from: https://upgrade.yubico.com/getapikey/");
        }

        // Validate OTP format (YubiKey OTP is typically 44 characters but can be 32-48)
        if (string.IsNullOrWhiteSpace(otp) || otp.Length < 32 || otp.Length > 48)
        {
            _logger.LogWarning("Invalid YubiKey OTP format for user {UserId}: length {Length}", userId, otp?.Length ?? 0);
            return false;
        }

        // Extract public ID from OTP (first 12 characters is standard, but can vary)
        // The public ID length is variable, but typically 12 chars. The OTP portion is always 32 chars.
        var publicId = otp.Length >= 44 ? otp.Substring(0, 12) : otp.Substring(0, otp.Length - 32);

        // Check for replay attack using cache
        var cacheKey = $"{YubiKeyOtpCachePrefix}{otp}";
        if (_cache.TryGetValue(cacheKey, out _))
        {
            _logger.LogWarning("YubiKey OTP replay attack detected for user {UserId}, OTP: {PublicId}...", userId, publicId);
            return false;
        }

        // Find user's hardware token device
        var device = await _context.MfaDevices
            .FirstOrDefaultAsync(d =>
                d.UserId == userId &&
                d.DeviceType == "HardwareToken" &&
                d.IsActive);

        if (device == null)
        {
            _logger.LogWarning("No active hardware token found for user {UserId}", userId);
            return false;
        }

        // Verify device fingerprint matches the public ID
        if (!string.IsNullOrEmpty(device.DeviceFingerprint) && device.DeviceFingerprint != publicId)
        {
            _logger.LogWarning(
                "YubiKey public ID mismatch for user {UserId}. Expected: {Expected}, Got: {Got}",
                userId, device.DeviceFingerprint, publicId);
            return false;
        }

        try
        {
            // Verify OTP with Yubico Validation API
            var (isValid, statusMessage) = await VerifyYubiKeyOtpWithApiAsync(clientId, secretKey, otp);

            if (!isValid)
            {
                _logger.LogWarning(
                    "YubiKey OTP verification failed for user {UserId}: {Status}",
                    userId, statusMessage);
                return false;
            }

            // Cache the validated OTP to prevent replay attacks
            _cache.Set(cacheKey, true, TimeSpan.FromSeconds(YubiKeyOtpCacheExpirationSeconds));

            // Update device last used timestamp
            device.LastUsedAt = DateTime.UtcNow;

            // Update device fingerprint if not set
            if (string.IsNullOrEmpty(device.DeviceFingerprint))
            {
                device.DeviceFingerprint = publicId;
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "YubiKey OTP verified successfully for user {UserId}, public ID: {PublicId}",
                userId, publicId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unexpected error during YubiKey OTP verification for user {UserId}",
                userId);
            return false;
        }
    }

    /// <summary>
    /// Verifies YubiKey OTP using Yubico Validation API v2.0
    /// </summary>
    private async Task<(bool IsValid, string StatusMessage)> VerifyYubiKeyOtpWithApiAsync(
        string clientId, string secretKey, string otp)
    {
        // Generate nonce (random value for preventing replay attacks)
        var nonce = Guid.NewGuid().ToString("N");

        // Build request parameters
        var parameters = new Dictionary<string, string>
        {
            { "id", clientId },
            { "otp", otp },
            { "nonce", nonce }
        };

        // Calculate HMAC signature
        var sortedParams = string.Join("&", parameters.OrderBy(p => p.Key).Select(p => $"{p.Key}={p.Value}"));
        var signature = CalculateHmacSha1(sortedParams, secretKey);
        parameters.Add("h", signature);

        // Get API URLs with fallback
        var apiUrls = _configuration.GetSection("MfaSettings:YubicoApiUrls").Get<string[]>()
            ?? new[] { "https://api.yubico.com/wsapi/2.0/verify" };

        var timeoutSeconds = _configuration.GetValue<int>("MfaSettings:YubicoTimeoutSeconds", 15);

        // Try each API URL until one succeeds
        foreach (var apiUrl in apiUrls)
        {
            try
            {
                var queryString = string.Join("&", parameters.Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value)}"));
                var requestUrl = $"{apiUrl}?{queryString}";

                using var httpClient = _httpClientFactory.CreateClient();
                httpClient.Timeout = TimeSpan.FromSeconds(timeoutSeconds);

                _logger.LogDebug("Calling Yubico API: {Url}", apiUrl);

                var response = await httpClient.GetStringAsync(requestUrl);

                // Parse response
                var responseDict = response.Split('\n')
                    .Where(line => line.Contains('='))
                    .Select(line => line.Split('='))
                    .ToDictionary(parts => parts[0].Trim(), parts => parts[1].Trim());

                if (!responseDict.TryGetValue("status", out var status))
                {
                    _logger.LogWarning("YubiKey API response missing status field");
                    continue;
                }

                // Verify response nonce matches request nonce
                if (responseDict.TryGetValue("nonce", out var responseNonce) && responseNonce != nonce)
                {
                    _logger.LogWarning("YubiKey API response nonce mismatch");
                    continue;
                }

                // Verify HMAC signature of response
                if (responseDict.TryGetValue("h", out var responseSignature))
                {
                    var responseParams = string.Join("&",
                        responseDict.Where(p => p.Key != "h")
                            .OrderBy(p => p.Key)
                            .Select(p => $"{p.Key}={p.Value}"));
                    var expectedSignature = CalculateHmacSha1(responseParams, secretKey);

                    if (responseSignature != expectedSignature)
                    {
                        _logger.LogWarning("YubiKey API response signature mismatch");
                        continue;
                    }
                }

                // Check status
                if (status == "OK")
                {
                    // Verify OTP matches
                    if (responseDict.TryGetValue("otp", out var responseOtp) && responseOtp != otp)
                    {
                        _logger.LogWarning("YubiKey API response OTP mismatch");
                        return (false, "OTP mismatch in response");
                    }

                    return (true, "OK");
                }
                else
                {
                    return (false, status);
                }
            }
            catch (TaskCanceledException)
            {
                _logger.LogWarning("YubiKey API timeout for URL: {Url}", apiUrl);
                continue;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "YubiKey API request failed for URL: {Url}", apiUrl);
                continue;
            }
        }

        return (false, "All API endpoints failed");
    }

    /// <summary>
    /// Calculates HMAC-SHA1 signature for Yubico API
    /// </summary>
    private string CalculateHmacSha1(string message, string secretKey)
    {
        var keyBytes = Convert.FromBase64String(secretKey);
        using var hmac = new HMACSHA1(keyBytes);
        var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
        return Convert.ToBase64String(hashBytes);
    }

    public async Task<bool> EnrollPushNotificationAsync(Guid userId, string deviceToken, string devicePlatform)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            throw new InvalidOperationException("User not found");
        }

        // Enable MFA if not already enabled
        if (!user.MfaEnabled)
        {
            user.MfaEnabled = true;
            await _userManager.UpdateAsync(user);
        }

        // Check if push device already exists for this platform
        var existingDevice = await _context.MfaDevices
            .FirstOrDefaultAsync(d => d.UserId == userId &&
                                     d.DeviceType == "Push" &&
                                     d.DevicePlatform == devicePlatform &&
                                     d.IsActive);

        if (existingDevice != null)
        {
            // Update device token
            existingDevice.PushToken = deviceToken;
            existingDevice.LastUsedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Updated push notification device token for user {UserId}, platform: {Platform}",
                userId, devicePlatform);
            return true;
        }

        // Create MFA device record
        var device = new MfaDevice
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            DeviceType = "Push",
            DeviceName = $"Push ({devicePlatform})",
            PushToken = deviceToken,
            DevicePlatform = devicePlatform,
            IsActive = true,
            IsPrimary = false,
            RegisteredAt = DateTime.UtcNow
        };

        _context.MfaDevices.Add(device);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Push notification enrolled for user {UserId}, platform: {Platform}",
            userId, devicePlatform);

        return true;
    }

    #region Private Helper Methods

    private bool VerifyTotpCode(string secret, string code)
    {
        try
        {
            var secretBytes = Base32Encoding.ToBytes(secret);
            var totp = new Totp(secretBytes);

            // Verify with time window (allow 1 step before and after for clock skew)
            return totp.VerifyTotp(code, out _, new VerificationWindow(1, 1));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying TOTP code");
            return false;
        }
    }

    private string GenerateQrCode(string otpAuthUrl)
    {
        try
        {
            using var qrGenerator = new QRCodeGenerator();
            using var qrCodeData = qrGenerator.CreateQrCode(otpAuthUrl, QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new PngByteQRCode(qrCodeData);
            var qrCodeBytes = qrCode.GetGraphic(20);

            return $"data:image/png;base64,{Convert.ToBase64String(qrCodeBytes)}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating QR code");
            throw;
        }
    }

    private string FormatSecretForManualEntry(string secret)
    {
        // Format as groups of 4 characters for easier manual entry
        var formatted = new StringBuilder();
        for (int i = 0; i < secret.Length; i++)
        {
            if (i > 0 && i % 4 == 0)
            {
                formatted.Append(' ');
            }
            formatted.Append(secret[i]);
        }
        return formatted.ToString();
    }

    private async Task<List<string>> GenerateBackupCodesInternalAsync(Guid userId)
    {
        var codes = new List<string>();

        for (int i = 0; i < BackupCodeCount; i++)
        {
            var code = GenerateBackupCode();
            codes.Add(code);

            var backupCode = new MfaBackupCode
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                CodeHash = HashBackupCode(code),
                IsUsed = false,
                CreatedAt = DateTime.UtcNow
            };

            _context.MfaBackupCodes.Add(backupCode);
        }

        await _context.SaveChangesAsync();
        return codes;
    }

    private string GenerateBackupCode()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var random = RandomNumberGenerator.Create();
        var bytes = new byte[BackupCodeLength];
        random.GetBytes(bytes);

        var code = new char[BackupCodeLength];
        for (int i = 0; i < BackupCodeLength; i++)
        {
            code[i] = chars[bytes[i] % chars.Length];
        }

        // Format as XXXX-XXXX
        return $"{new string(code, 0, 4)}-{new string(code, 4, 4)}";
    }

    private string HashBackupCode(string code)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(code.ToUpperInvariant().Replace("-", ""));
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }

    private static string GenerateSixDigitCode()
    {
        var bytes = new byte[3];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);

        var code = BitConverter.ToUInt32(new byte[] { bytes[0], bytes[1], bytes[2], 0 }) % 1000000;
        return code.ToString("D6");
    }

    private static string MaskPhoneNumber(string phoneNumber)
    {
        if (string.IsNullOrEmpty(phoneNumber) || phoneNumber.Length < 4)
        {
            return "****";
        }

        return $"****{phoneNumber.Substring(phoneNumber.Length - 4)}";
    }

    private static string GenerateMagicToken()
    {
        var bytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").Replace("=", "");
    }

    #endregion
}
