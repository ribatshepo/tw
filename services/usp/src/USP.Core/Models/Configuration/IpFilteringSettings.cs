namespace USP.Core.Models.Configuration;

/// <summary>
/// IP filtering configuration settings
/// </summary>
public class IpFilteringSettings
{
    /// <summary>
    /// Enable IP whitelist (if enabled, only whitelisted IPs are allowed)
    /// </summary>
    public bool EnableWhitelist { get; set; } = false;

    /// <summary>
    /// Enable IP blacklist (if enabled, blacklisted IPs are denied)
    /// </summary>
    public bool EnableBlacklist { get; set; } = true;

    /// <summary>
    /// Enable geo-blocking
    /// </summary>
    public bool EnableGeoBlocking { get; set; } = false;

    /// <summary>
    /// Temporary ban duration in minutes for threshold violations
    /// </summary>
    public int TemporaryBanDurationMinutes { get; set; } = 15;

    /// <summary>
    /// Number of failed authentication attempts before temporary ban
    /// </summary>
    public int FailedAttemptsBeforeBan { get; set; } = 5;

    /// <summary>
    /// Time window in minutes for counting failed attempts
    /// </summary>
    public int FailedAttemptsWindowMinutes { get; set; } = 10;

    /// <summary>
    /// Whitelist IP addresses (CIDR notation supported)
    /// </summary>
    public List<string> WhitelistIps { get; set; } = new();

    /// <summary>
    /// Blacklist IP addresses (CIDR notation supported)
    /// </summary>
    public List<string> BlacklistIps { get; set; } = new();

    /// <summary>
    /// Blocked country codes (ISO 3166-1 alpha-2, e.g., CN, RU)
    /// </summary>
    public List<string> BlockedCountries { get; set; } = new();

    /// <summary>
    /// MaxMind GeoIP2 database path for geo-blocking
    /// </summary>
    public string GeoIp2DatabasePath { get; set; } = "/app/data/GeoLite2-Country.mmdb";

    /// <summary>
    /// Validates IP filtering configuration settings
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when validation fails</exception>
    public void Validate()
    {
        if (TemporaryBanDurationMinutes <= 0)
        {
            throw new InvalidOperationException("Temporary ban duration must be positive");
        }

        if (FailedAttemptsBeforeBan <= 0)
        {
            throw new InvalidOperationException("Failed attempts before ban must be positive");
        }

        if (FailedAttemptsWindowMinutes <= 0)
        {
            throw new InvalidOperationException("Failed attempts window minutes must be positive");
        }

        if (EnableGeoBlocking && string.IsNullOrWhiteSpace(GeoIp2DatabasePath))
        {
            throw new InvalidOperationException("GeoIP2 database path is required when geo-blocking is enabled");
        }
    }
}
