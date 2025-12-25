using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using USP.Core.Models.DTOs.Device;
using USP.Core.Services.Device;

namespace USP.Infrastructure.Services.Device;

/// <summary>
/// Geolocation service with multi-provider support and caching
/// Supports: ipapi.co, ip-api.com, MaxMind GeoIP2 (configurable)
/// </summary>
public class GeolocationService : IGeolocationService
{
    private readonly ILogger<GeolocationService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly IConfiguration _configuration;

    private const string CacheKeyPrefix = "geo:";
    private const int CacheExpirationMinutes = 60; // Cache IP locations for 1 hour
    private const int RequestTimeoutSeconds = 5;

    public GeolocationService(
        ILogger<GeolocationService> logger,
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        IConfiguration configuration)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _configuration = configuration;
    }

    public async Task<GeolocationResult?> GetLocationFromIpAsync(string ipAddress)
    {
        // Skip private/local IP addresses
        if (IsPrivateOrLocalIp(ipAddress))
        {
            _logger.LogDebug("Skipping geolocation for private/local IP: {IpAddress}", ipAddress);
            return new GeolocationResult
            {
                IpAddress = ipAddress,
                Country = "Local",
                CountryCode = "XX",
                City = "Local Network",
                Provider = "Local"
            };
        }

        // Check cache first
        var cacheKey = $"{CacheKeyPrefix}{ipAddress}";
        if (_cache.TryGetValue<GeolocationResult>(cacheKey, out var cachedResult))
        {
            _logger.LogDebug("Returning cached geolocation for IP: {IpAddress}", ipAddress);
            return cachedResult;
        }

        // Try providers in order
        var result = await TryIpApiCo(ipAddress)
                     ?? await TryIpApi(ipAddress);

        if (result != null)
        {
            // Cache successful result
            _cache.Set(cacheKey, result, TimeSpan.FromMinutes(CacheExpirationMinutes));
            _logger.LogInformation("Geolocation found for IP {IpAddress}: {City}, {Country}",
                ipAddress, result.City, result.Country);
        }
        else
        {
            _logger.LogWarning("Failed to get geolocation for IP: {IpAddress}", ipAddress);
        }

        return result;
    }

    public double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
    {
        const double earthRadiusKm = 6371.0;

        var dLat = DegreesToRadians(lat2 - lat1);
        var dLon = DegreesToRadians(lon2 - lon1);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(DegreesToRadians(lat1)) * Math.Cos(DegreesToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return earthRadiusKm * c;
    }

    public bool IsImpossibleTravel(double lat1, double lon1, double lat2, double lon2, double timeElapsedHours, double maxRealisticSpeedKmh = 800)
    {
        if (timeElapsedHours <= 0)
        {
            return false;
        }

        var distanceKm = CalculateDistance(lat1, lon1, lat2, lon2);
        var requiredSpeedKmh = distanceKm / timeElapsedHours;

        if (requiredSpeedKmh > maxRealisticSpeedKmh)
        {
            _logger.LogWarning("Impossible travel detected: {DistanceKm} km in {Hours} hours (speed: {SpeedKmh} km/h)",
                distanceKm, timeElapsedHours, requiredSpeedKmh);
            return true;
        }

        return false;
    }

    #region Private Helper Methods

    private async Task<GeolocationResult?> TryIpApiCo(string ipAddress)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(RequestTimeoutSeconds);

            var response = await client.GetAsync($"https://ipapi.co/{ipAddress}/json/");

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug("ipapi.co returned status {StatusCode} for IP {IpAddress}",
                    response.StatusCode, ipAddress);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<IpapiCoResponse>(json);

            if (data == null || data.Error)
            {
                return null;
            }

            return new GeolocationResult
            {
                IpAddress = ipAddress,
                Country = data.CountryName ?? string.Empty,
                CountryCode = data.CountryCode ?? string.Empty,
                Region = data.Region ?? string.Empty,
                City = data.City ?? string.Empty,
                Latitude = data.Latitude,
                Longitude = data.Longitude,
                Timezone = data.Timezone ?? string.Empty,
                Provider = "ipapi.co"
            };
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to get geolocation from ipapi.co for IP {IpAddress}", ipAddress);
            return null;
        }
    }

    private async Task<GeolocationResult?> TryIpApi(string ipAddress)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(RequestTimeoutSeconds);

            var response = await client.GetAsync($"http://ip-api.com/json/{ipAddress}?fields=status,message,country,countryCode,region,regionName,city,lat,lon,timezone,proxy");

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug("ip-api.com returned status {StatusCode} for IP {IpAddress}",
                    response.StatusCode, ipAddress);
                return null;
            }

            var data = await response.Content.ReadFromJsonAsync<IpApiResponse>();

            if (data == null || data.Status != "success")
            {
                return null;
            }

            return new GeolocationResult
            {
                IpAddress = ipAddress,
                Country = data.Country ?? string.Empty,
                CountryCode = data.CountryCode ?? string.Empty,
                Region = data.RegionName ?? string.Empty,
                City = data.City ?? string.Empty,
                Latitude = data.Lat,
                Longitude = data.Lon,
                Timezone = data.Timezone ?? string.Empty,
                IsProxy = data.Proxy,
                Provider = "ip-api.com"
            };
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to get geolocation from ip-api.com for IP {IpAddress}", ipAddress);
            return null;
        }
    }

    private static bool IsPrivateOrLocalIp(string ipAddress)
    {
        if (string.IsNullOrWhiteSpace(ipAddress))
        {
            return true;
        }

        // Localhost
        if (ipAddress == "127.0.0.1" || ipAddress == "::1" || ipAddress.StartsWith("127."))
        {
            return true;
        }

        // Private ranges
        var octets = ipAddress.Split('.');
        if (octets.Length == 4)
        {
            if (int.TryParse(octets[0], out var first))
            {
                // 10.0.0.0/8
                if (first == 10)
                {
                    return true;
                }

                // 172.16.0.0/12
                if (first == 172 && int.TryParse(octets[1], out var second) && second >= 16 && second <= 31)
                {
                    return true;
                }

                // 192.168.0.0/16
                if (first == 192 && int.TryParse(octets[1], out var second2) && second2 == 168)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static double DegreesToRadians(double degrees)
    {
        return degrees * Math.PI / 180.0;
    }

    #endregion

    #region Provider Response DTOs

    private class IpapiCoResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("country_name")]
        public string? CountryName { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("country_code")]
        public string? CountryCode { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("region")]
        public string? Region { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("city")]
        public string? City { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("latitude")]
        public double Latitude { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("longitude")]
        public double Longitude { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("timezone")]
        public string? Timezone { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("error")]
        public bool Error { get; set; }
    }

    private class IpApiResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("status")]
        public string? Status { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("message")]
        public string? Message { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("country")]
        public string? Country { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("countryCode")]
        public string? CountryCode { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("region")]
        public string? Region { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("regionName")]
        public string? RegionName { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("city")]
        public string? City { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("lat")]
        public double Lat { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("lon")]
        public double Lon { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("timezone")]
        public string? Timezone { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("proxy")]
        public bool Proxy { get; set; }
    }

    #endregion
}
