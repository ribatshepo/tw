using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using USP.Core.Models.DTOs.Device;
using USP.Core.Models.Entities;
using USP.Core.Services.Device;
using USP.Infrastructure.Data;

namespace USP.Infrastructure.Services.Device;

/// <summary>
/// Service for device fingerprinting and trusted device management
/// </summary>
public class DeviceFingerprintService : IDeviceFingerprintService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<DeviceFingerprintService> _logger;
    private const int TrustDurationDays = 30;

    public DeviceFingerprintService(
        ApplicationDbContext context,
        ILogger<DeviceFingerprintService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public string GenerateFingerprint(string userAgent, string ipAddress, Dictionary<string, string>? additionalData = null)
    {
        var fingerprintData = new
        {
            UserAgent = userAgent,
            IpAddress = ipAddress,
            AdditionalData = additionalData ?? new Dictionary<string, string>()
        };

        var json = JsonSerializer.Serialize(fingerprintData);
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(json));
        return Convert.ToBase64String(hashBytes);
    }

    public async Task<TrustedDeviceDto> RegisterTrustedDeviceAsync(Guid userId, string deviceFingerprint, string deviceName)
    {
        var existingDevice = await _context.TrustedDevices
            .FirstOrDefaultAsync(d => d.UserId == userId && d.DeviceFingerprint == deviceFingerprint && d.IsActive);

        if (existingDevice != null)
        {
            existingDevice.LastUsedAt = DateTime.UtcNow;
            existingDevice.ExpiresAt = DateTime.UtcNow.AddDays(TrustDurationDays);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Updated existing trusted device for user {UserId}", userId);

            return MapToDto(existingDevice);
        }

        var device = new TrustedDevice
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            DeviceFingerprint = deviceFingerprint,
            DeviceName = deviceName,
            DeviceType = "Browser",
            RegisteredAt = DateTime.UtcNow,
            LastUsedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(TrustDurationDays),
            IsActive = true
        };

        _context.TrustedDevices.Add(device);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Registered new trusted device for user {UserId}", userId);

        return MapToDto(device);
    }

    public async Task<bool> IsTrustedDeviceAsync(Guid userId, string deviceFingerprint)
    {
        var device = await _context.TrustedDevices
            .FirstOrDefaultAsync(d =>
                d.UserId == userId &&
                d.DeviceFingerprint == deviceFingerprint &&
                d.IsActive &&
                d.ExpiresAt > DateTime.UtcNow);

        return device != null;
    }

    public async Task<IEnumerable<TrustedDeviceDto>> GetTrustedDevicesAsync(Guid userId)
    {
        var devices = await _context.TrustedDevices
            .Where(d => d.UserId == userId && d.IsActive)
            .OrderByDescending(d => d.LastUsedAt)
            .ToListAsync();

        return devices.Select(MapToDto);
    }

    public async Task<bool> RemoveTrustedDeviceAsync(Guid userId, Guid deviceId)
    {
        var device = await _context.TrustedDevices
            .FirstOrDefaultAsync(d => d.Id == deviceId && d.UserId == userId);

        if (device == null)
        {
            return false;
        }

        device.IsActive = false;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Removed trusted device {DeviceId} for user {UserId}", deviceId, userId);

        return true;
    }

    public async Task UpdateDeviceLastUsedAsync(Guid userId, string deviceFingerprint)
    {
        var device = await _context.TrustedDevices
            .FirstOrDefaultAsync(d =>
                d.UserId == userId &&
                d.DeviceFingerprint == deviceFingerprint &&
                d.IsActive);

        if (device != null)
        {
            device.LastUsedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    private static TrustedDeviceDto MapToDto(TrustedDevice device) => new()
    {
        Id = device.Id,
        DeviceName = device.DeviceName,
        DeviceType = device.DeviceType,
        RegisteredAt = device.RegisteredAt,
        LastUsedAt = device.LastUsedAt,
        IpAddress = device.IpAddress,
        Location = device.Location
    };
}
