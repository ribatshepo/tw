namespace USP.Core.Models.DTOs.Device;

/// <summary>
/// Geolocation information from IP address lookup
/// </summary>
public class GeolocationResult
{
    public string Country { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string Timezone { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public bool IsProxy { get; set; }
    public bool IsVpn { get; set; }
    public bool IsTor { get; set; }
    public string Provider { get; set; } = string.Empty;
    public DateTime RetrievedAt { get; set; } = DateTime.UtcNow;
}
