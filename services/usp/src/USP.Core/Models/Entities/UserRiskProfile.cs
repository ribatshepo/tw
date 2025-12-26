namespace USP.Core.Models.Entities;

/// <summary>
/// User risk profile for baseline behavioral tracking
/// </summary>
public class UserRiskProfile
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    // Behavioral baseline
    public List<string> KnownIpAddresses { get; set; } = new();
    public List<string> KnownCountries { get; set; } = new();
    public List<string> KnownCities { get; set; } = new();
    public List<string> KnownDeviceFingerprints { get; set; } = new();
    public List<int> TypicalLoginHours { get; set; } = new(); // Hour of day (0-23)

    // Risk metrics
    public int BaselineRiskScore { get; set; } = 0; // 0-100
    public int CurrentRiskScore { get; set; } = 0; // 0-100
    public string RiskTier { get; set; } = "normal"; // low, normal, elevated, high, critical

    // Anomaly detection
    public int ConsecutiveFailedLogins { get; set; } = 0;
    public int SuspiciousActivityCount { get; set; } = 0;
    public DateTime? LastSuspiciousActivity { get; set; }
    public DateTime? LastPasswordChange { get; set; }
    public DateTime? LastMfaEnrollment { get; set; }

    // Geographic tracking
    public string? LastKnownCountry { get; set; }
    public string? LastKnownCity { get; set; }
    public double? LastKnownLatitude { get; set; }
    public double? LastKnownLongitude { get; set; }
    public DateTime? LastLocationUpdate { get; set; }

    // Velocity tracking
    public int LoginAttemptsLast24Hours { get; set; } = 0;
    public int LoginAttemptsLastHour { get; set; } = 0;
    public DateTime? LastLoginAttempt { get; set; }

    // Trust level
    public int TrustScore { get; set; } = 50; // 0-100, starts at neutral
    public bool IsCompromised { get; set; } = false;
    public bool RequiresMandatoryMfa { get; set; } = false;

    // Metadata
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastCalculatedAt { get; set; }

    // Navigation property
    public ApplicationUser User { get; set; } = null!;
}
