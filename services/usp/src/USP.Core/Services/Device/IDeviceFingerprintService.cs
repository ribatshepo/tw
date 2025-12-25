using USP.Core.Models.DTOs.Device;

namespace USP.Core.Services.Device;

/// <summary>
/// Service for device fingerprinting and trusted device management
/// </summary>
public interface IDeviceFingerprintService
{
    /// <summary>
    /// Generate device fingerprint from request context
    /// </summary>
    string GenerateFingerprint(string userAgent, string ipAddress, Dictionary<string, string>? additionalData = null);

    /// <summary>
    /// Register a trusted device for user
    /// </summary>
    Task<TrustedDeviceDto> RegisterTrustedDeviceAsync(Guid userId, string deviceFingerprint, string deviceName);

    /// <summary>
    /// Check if device is trusted for user
    /// </summary>
    Task<bool> IsTrustedDeviceAsync(Guid userId, string deviceFingerprint);

    /// <summary>
    /// Get user's trusted devices
    /// </summary>
    Task<IEnumerable<TrustedDeviceDto>> GetTrustedDevicesAsync(Guid userId);

    /// <summary>
    /// Remove trusted device
    /// </summary>
    Task<bool> RemoveTrustedDeviceAsync(Guid userId, Guid deviceId);

    /// <summary>
    /// Update device last used timestamp
    /// </summary>
    Task UpdateDeviceLastUsedAsync(Guid userId, string deviceFingerprint);

    /// <summary>
    /// Update device location from IP address
    /// </summary>
    Task UpdateDeviceLocationAsync(Guid userId, string deviceFingerprint, string ipAddress);

    /// <summary>
    /// Detect impossible travel between last known location and new location
    /// </summary>
    /// <returns>True if travel is impossible</returns>
    Task<bool> DetectImpossibleTravelAsync(Guid userId, string ipAddress);

    /// <summary>
    /// Get geographic risk score based on device location
    /// Higher score = higher risk (0-100)
    /// </summary>
    Task<int> GetGeographicRiskScoreAsync(Guid userId, string ipAddress);
}
