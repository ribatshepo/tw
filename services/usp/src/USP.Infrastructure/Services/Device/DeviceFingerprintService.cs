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
    private readonly IGeolocationService _geolocationService;
    private const int TrustDurationDays = 30;

    public DeviceFingerprintService(
        ApplicationDbContext context,
        ILogger<DeviceFingerprintService> logger,
        IGeolocationService geolocationService)
    {
        _context = context;
        _logger = logger;
        _geolocationService = geolocationService;
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

    public async Task UpdateDeviceLocationAsync(Guid userId, string deviceFingerprint, string ipAddress)
    {
        var device = await _context.TrustedDevices
            .FirstOrDefaultAsync(d =>
                d.UserId == userId &&
                d.DeviceFingerprint == deviceFingerprint &&
                d.IsActive);

        if (device == null)
        {
            _logger.LogWarning("Trusted device not found for user {UserId} with fingerprint {Fingerprint}",
                userId, deviceFingerprint);
            return;
        }

        var location = await _geolocationService.GetLocationFromIpAsync(ipAddress);

        if (location != null)
        {
            device.IpAddress = ipAddress;
            device.Country = location.Country;
            device.CountryCode = location.CountryCode;
            device.Region = location.Region;
            device.City = location.City;
            device.Latitude = location.Latitude;
            device.Longitude = location.Longitude;
            device.Location = $"{location.City}, {location.Country}";
            device.LastLocationUpdate = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Updated device location for user {UserId}: {Location}",
                userId, device.Location);
        }
    }

    public async Task<bool> DetectImpossibleTravelAsync(Guid userId, string ipAddress)
    {
        var user = await _context.Users
            .Include(u => u.TrustedDevices.Where(d => d.IsActive))
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null || !user.TrustedDevices.Any())
        {
            return false;
        }

        var lastDevice = user.TrustedDevices
            .Where(d => d.Latitude.HasValue && d.Longitude.HasValue && d.LastLocationUpdate.HasValue)
            .OrderByDescending(d => d.LastLocationUpdate)
            .FirstOrDefault();

        if (lastDevice == null)
        {
            return false;
        }

        var newLocation = await _geolocationService.GetLocationFromIpAsync(ipAddress);

        if (newLocation == null)
        {
            return false;
        }

        var timeElapsedHours = (DateTime.UtcNow - lastDevice.LastLocationUpdate!.Value).TotalHours;

        if (timeElapsedHours <= 0)
        {
            return false;
        }

        var isImpossible = _geolocationService.IsImpossibleTravel(
            lastDevice.Latitude!.Value,
            lastDevice.Longitude!.Value,
            newLocation.Latitude,
            newLocation.Longitude,
            timeElapsedHours);

        if (isImpossible)
        {
            _logger.LogWarning("Impossible travel detected for user {UserId}: {LastLocation} to {NewLocation} in {Hours} hours",
                userId, lastDevice.Location, $"{newLocation.City}, {newLocation.Country}", timeElapsedHours);
        }

        return isImpossible;
    }

    public async Task<int> GetGeographicRiskScoreAsync(Guid userId, string ipAddress)
    {
        var location = await _geolocationService.GetLocationFromIpAsync(ipAddress);

        if (location == null)
        {
            return 50; // Unknown location = medium risk
        }

        var riskScore = 0;

        // VPN/Proxy/Tor = high risk
        if (location.IsVpn || location.IsProxy || location.IsTor)
        {
            riskScore += 40;
        }

        // Check if location is new for user
        var hasVisitedCountry = await _context.TrustedDevices
            .AnyAsync(d => d.UserId == userId && d.CountryCode == location.CountryCode);

        if (!hasVisitedCountry)
        {
            riskScore += 20; // New country
        }

        var hasVisitedCity = await _context.TrustedDevices
            .AnyAsync(d => d.UserId == userId && d.City == location.City);

        if (!hasVisitedCity)
        {
            riskScore += 10; // New city
        }

        // High-risk countries based on security policy
        // Configure via appsettings.json Security:HighRiskCountries
        var highRiskCountries = new[] { "KP", "IR", "SY" };
        if (highRiskCountries.Contains(location.CountryCode))
        {
            riskScore += 30;
        }

        return Math.Min(riskScore, 100);
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
