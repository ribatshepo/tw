using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OtpNet;
using QRCoder;
using System.Security.Cryptography;
using System.Text;
using USP.Core.Models.DTOs.Mfa;
using USP.Core.Models.Entities;
using USP.Core.Services.Communication;
using USP.Core.Services.Mfa;
using USP.Infrastructure.Data;

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

    private const int BackupCodeCount = 10;
    private const int BackupCodeLength = 8;
    private const string PhoneVerificationCodeCachePrefix = "mfa:phone:verify:";
    private const string SmsOtpCodeCachePrefix = "mfa:sms:otp:";
    private const int VerificationCodeExpirationMinutes = 10;
    private const int SmsOtpExpirationMinutes = 5;

    public MfaService(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration,
        ILogger<MfaService> logger,
        ISmsService smsService,
        IMemoryCache cache)
    {
        _context = context;
        _userManager = userManager;
        _configuration = configuration;
        _logger = logger;
        _smsService = smsService;
        _cache = cache;
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

    #endregion
}
