using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using USP.Core.Domain.Entities.Identity;
using USP.Core.Domain.Enums;
using USP.Core.Interfaces.Services;
using USP.Core.Interfaces.Services.Authentication;
using USP.Infrastructure.Persistence;

namespace USP.Infrastructure.Services.Authentication;

/// <summary>
/// Main MFA orchestration service implementation
/// </summary>
public class MFAService : IMFAService
{
    private readonly ApplicationDbContext _context;
    private readonly ITOTPService _totpService;
    private readonly IBackupCodesService _backupCodesService;
    private readonly IEmailService _emailService;
    private readonly ILogger<MFAService> _logger;

    public MFAService(
        ApplicationDbContext context,
        ITOTPService totpService,
        IBackupCodesService backupCodesService,
        IEmailService emailService,
        ILogger<MFAService> logger)
    {
        _context = context;
        _totpService = totpService;
        _backupCodesService = backupCodesService;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<MFADevice> EnrollDeviceAsync(
        string userId,
        MFAMethod method,
        string deviceName,
        CancellationToken cancellationToken = default)
    {
        var device = new MFADevice
        {
            UserId = userId,
            Method = method,
            DeviceName = deviceName,
            IsVerified = false,
            IsPrimary = false,
            EnrolledAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Generate method-specific data
        switch (method)
        {
            case MFAMethod.TOTP:
                var secret = _totpService.GenerateSecret();
                device.DeviceData = secret;
                break;

            case MFAMethod.BackupCode:
                var codes = _backupCodesService.GenerateBackupCodes(10);
                var hashedCodes = codes.Select(c => _backupCodesService.HashBackupCode(c)).ToList();
                device.DeviceData = string.Join(",", hashedCodes);
                break;

            case MFAMethod.Email:
            case MFAMethod.SMS:
            case MFAMethod.Push:
            case MFAMethod.WebAuthn:
                // These methods require additional setup and will be verified separately
                break;

            default:
                throw new ArgumentException($"Unsupported MFA method: {method}");
        }

        _context.Set<MFADevice>().Add(device);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("MFA device enrolled for user {UserId}: {Method} - {DeviceId}",
            userId, method, device.Id);

        return device;
    }

    public async Task<bool> VerifyEnrollmentAsync(
        string userId,
        string deviceId,
        string code,
        CancellationToken cancellationToken = default)
    {
        var device = await _context.Set<MFADevice>()
            .FirstOrDefaultAsync(d => d.Id == deviceId && d.UserId == userId && !d.IsVerified, cancellationToken);

        if (device == null)
        {
            return false;
        }

        bool isValid = false;

        switch (device.Method)
        {
            case MFAMethod.TOTP:
                if (device.DeviceData != null)
                {
                    isValid = _totpService.VerifyCode(device.DeviceData, code);
                }
                break;

            case MFAMethod.Email:
            case MFAMethod.SMS:
                // Verify OTP code stored temporarily
                isValid = device.DeviceData == code;
                break;

            case MFAMethod.BackupCode:
                // Backup codes are verified during use, not enrollment
                isValid = true;
                break;

            default:
                return false;
        }

        if (isValid)
        {
            device.IsVerified = true;
            device.UpdatedAt = DateTime.UtcNow;

            // If this is the first MFA device, make it primary
            var hasPrimary = await _context.Set<MFADevice>()
                .AnyAsync(d => d.UserId == userId && d.IsPrimary && d.Id != deviceId, cancellationToken);

            if (!hasPrimary)
            {
                device.IsPrimary = true;
            }

            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("MFA device verified for user {UserId}: {DeviceId}", userId, deviceId);
        }

        return isValid;
    }

    public async Task<bool> VerifyMFACodeAsync(
        string userId,
        MFAMethod method,
        string code,
        CancellationToken cancellationToken = default)
    {
        var devices = await _context.Set<MFADevice>()
            .Where(d => d.UserId == userId && d.Method == method && d.IsVerified && d.DeletedAt == null)
            .ToListAsync(cancellationToken);

        if (!devices.Any())
        {
            return false;
        }

        foreach (var device in devices)
        {
            bool isValid = false;

            switch (method)
            {
                case MFAMethod.TOTP:
                    if (device.DeviceData != null)
                    {
                        isValid = _totpService.VerifyCode(device.DeviceData, code);
                    }
                    break;

                case MFAMethod.BackupCode:
                    if (device.DeviceData != null)
                    {
                        var hashedCodes = device.DeviceData.Split(',').ToList();
                        foreach (var hash in hashedCodes)
                        {
                            if (_backupCodesService.VerifyBackupCode(code, hash))
                            {
                                // Remove used backup code
                                hashedCodes.Remove(hash);
                                device.DeviceData = string.Join(",", hashedCodes);
                                isValid = true;
                                break;
                            }
                        }
                    }
                    break;

                case MFAMethod.Email:
                case MFAMethod.SMS:
                    // Verify OTP code sent via email/SMS
                    isValid = device.DeviceData == code;
                    break;

                default:
                    continue;
            }

            if (isValid)
            {
                device.LastUsedAt = DateTime.UtcNow;
                device.UsageCount++;
                device.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("MFA code verified for user {UserId} using {Method}", userId, method);
                return true;
            }
        }

        _logger.LogWarning("MFA code verification failed for user {UserId} using {Method}", userId, method);
        return false;
    }

    public async Task<List<MFADevice>> GetUserMFADevicesAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Set<MFADevice>()
            .Where(d => d.UserId == userId && d.DeletedAt == null)
            .OrderByDescending(d => d.IsPrimary)
            .ThenByDescending(d => d.LastUsedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task RemoveMFADeviceAsync(
        string userId,
        string deviceId,
        CancellationToken cancellationToken = default)
    {
        var device = await _context.Set<MFADevice>()
            .FirstOrDefaultAsync(d => d.Id == deviceId && d.UserId == userId && d.DeletedAt == null, cancellationToken)
            ?? throw new InvalidOperationException("MFA device not found");

        // Soft delete
        device.DeletedAt = DateTime.UtcNow;
        device.UpdatedAt = DateTime.UtcNow;

        // If removing primary device, assign another device as primary
        if (device.IsPrimary)
        {
            var nextDevice = await _context.Set<MFADevice>()
                .Where(d => d.UserId == userId && d.Id != deviceId && d.DeletedAt == null && d.IsVerified)
                .OrderByDescending(d => d.LastUsedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (nextDevice != null)
            {
                nextDevice.IsPrimary = true;
                nextDevice.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("MFA device removed for user {UserId}: {DeviceId}", userId, deviceId);
    }

    public async Task SetPrimaryDeviceAsync(
        string userId,
        string deviceId,
        CancellationToken cancellationToken = default)
    {
        var device = await _context.Set<MFADevice>()
            .FirstOrDefaultAsync(d => d.Id == deviceId && d.UserId == userId && d.IsVerified && d.DeletedAt == null, cancellationToken)
            ?? throw new InvalidOperationException("MFA device not found or not verified");

        // Remove primary flag from other devices
        var otherDevices = await _context.Set<MFADevice>()
            .Where(d => d.UserId == userId && d.Id != deviceId && d.IsPrimary)
            .ToListAsync(cancellationToken);

        foreach (var otherDevice in otherDevices)
        {
            otherDevice.IsPrimary = false;
            otherDevice.UpdatedAt = DateTime.UtcNow;
        }

        device.IsPrimary = true;
        device.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Primary MFA device set for user {UserId}: {DeviceId}", userId, deviceId);
    }

    public async Task<List<string>> GenerateBackupCodesAsync(
        string userId,
        int count = 10,
        CancellationToken cancellationToken = default)
    {
        var codes = _backupCodesService.GenerateBackupCodes(count);

        // Check if backup code device exists
        var existingDevice = await _context.Set<MFADevice>()
            .FirstOrDefaultAsync(d => d.UserId == userId && d.Method == MFAMethod.BackupCode && d.DeletedAt == null, cancellationToken);

        if (existingDevice != null)
        {
            // Update existing backup codes
            var hashedCodes = codes.Select(c => _backupCodesService.HashBackupCode(c)).ToList();
            existingDevice.DeviceData = string.Join(",", hashedCodes);
            existingDevice.IsVerified = true;
            existingDevice.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            // Create new backup codes device
            var hashedCodes = codes.Select(c => _backupCodesService.HashBackupCode(c)).ToList();
            var device = new MFADevice
            {
                UserId = userId,
                Method = MFAMethod.BackupCode,
                DeviceName = "Backup Codes",
                DeviceData = string.Join(",", hashedCodes),
                IsVerified = true,
                IsPrimary = false,
                EnrolledAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Set<MFADevice>().Add(device);
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Backup codes generated for user {UserId}", userId);

        return codes;
    }

    public async Task<TrustedDevice> TrustDeviceAsync(
        string userId,
        string deviceFingerprint,
        string deviceName,
        string ipAddress,
        string userAgent,
        int trustDays = 30,
        CancellationToken cancellationToken = default)
    {
        var trustedDevice = new TrustedDevice
        {
            UserId = userId,
            DeviceFingerprint = deviceFingerprint,
            DeviceName = deviceName,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            DeviceType = DeviceType.Unknown, // Could be determined from user agent
            IsActive = true,
            TrustedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(trustDays),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Set<TrustedDevice>().Add(trustedDevice);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Device trusted for user {UserId}: {DeviceId}", userId, trustedDevice.Id);

        return trustedDevice;
    }

    public async Task<bool> IsDeviceTrustedAsync(
        string userId,
        string deviceFingerprint,
        CancellationToken cancellationToken = default)
    {
        var trustedDevice = await _context.Set<TrustedDevice>()
            .FirstOrDefaultAsync(d =>
                d.UserId == userId &&
                d.DeviceFingerprint == deviceFingerprint &&
                d.IsActive &&
                d.DeletedAt == null,
                cancellationToken);

        if (trustedDevice == null)
        {
            return false;
        }

        if (trustedDevice.ExpiresAt.HasValue && trustedDevice.ExpiresAt.Value <= DateTime.UtcNow)
        {
            return false;
        }

        // Update last used timestamp
        trustedDevice.LastUsedAt = DateTime.UtcNow;
        trustedDevice.UsageCount++;
        trustedDevice.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<List<TrustedDevice>> GetTrustedDevicesAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Set<TrustedDevice>()
            .Where(d => d.UserId == userId && d.IsActive && d.DeletedAt == null)
            .OrderByDescending(d => d.LastUsedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task RevokeTrustedDeviceAsync(
        string userId,
        string deviceId,
        CancellationToken cancellationToken = default)
    {
        var device = await _context.Set<TrustedDevice>()
            .FirstOrDefaultAsync(d => d.Id == deviceId && d.UserId == userId && d.DeletedAt == null, cancellationToken)
            ?? throw new InvalidOperationException("Trusted device not found");

        device.IsActive = false;
        device.DeletedAt = DateTime.UtcNow;
        device.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Trusted device revoked for user {UserId}: {DeviceId}", userId, deviceId);
    }
}
