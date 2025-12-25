using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OtpNet;
using QRCoder;
using System.Security.Cryptography;
using System.Text;
using USP.Core.Models.DTOs.Mfa;
using USP.Core.Models.Entities;
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
    private const int BackupCodeCount = 10;
    private const int BackupCodeLength = 8;

    public MfaService(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration,
        ILogger<MfaService> logger)
    {
        _context = context;
        _userManager = userManager;
        _configuration = configuration;
        _logger = logger;
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

    #endregion
}
