using USP.Core.Models.DTOs.Device;

namespace USP.Core.Services.Device;

/// <summary>
/// Service for IP geolocation with multi-provider support
/// </summary>
public interface IGeolocationService
{
    /// <summary>
    /// Get geolocation information from IP address
    /// </summary>
    Task<GeolocationResult?> GetLocationFromIpAsync(string ipAddress);

    /// <summary>
    /// Calculate distance in kilometers between two coordinates
    /// </summary>
    double CalculateDistance(double lat1, double lon1, double lat2, double lon2);

    /// <summary>
    /// Check if travel between two locations is impossible given time elapsed
    /// </summary>
    /// <param name="lat1">Starting latitude</param>
    /// <param name="lon1">Starting longitude</param>
    /// <param name="lat2">Ending latitude</param>
    /// <param name="lon2">Ending longitude</param>
    /// <param name="timeElapsedHours">Time elapsed in hours</param>
    /// <param name="maxRealisticSpeedKmh">Maximum realistic speed (default: 800 km/h for commercial aircraft)</param>
    /// <returns>True if travel is impossible</returns>
    bool IsImpossibleTravel(double lat1, double lon1, double lat2, double lon2, double timeElapsedHours, double maxRealisticSpeedKmh = 800);
}
