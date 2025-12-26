namespace USP.Core.Models.Entities;

/// <summary>
/// User behavior analytics profile for ML-based anomaly detection
/// </summary>
public class UserBehaviorProfile
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public DateTime ProfileStartDate { get; set; } = DateTime.UtcNow;
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    public int SampleSize { get; set; } // Number of data points used for profiling

    // Temporal patterns
    public string TypicalLoginHours { get; set; } = string.Empty; // JSON array of hour bins
    public string TypicalLoginDaysOfWeek { get; set; } = string.Empty; // JSON array
    public int AverageLoginsPerDay { get; set; }
    public int AverageLoginsPerWeek { get; set; }
    public double LoginFrequencyStdDev { get; set; }

    // Geolocation patterns
    public string CommonLocations { get; set; } = string.Empty; // JSON array of {country, city, lat, lon, frequency}
    public string CommonIpRanges { get; set; } = string.Empty; // JSON array of IP CIDR ranges
    public double GeographicDiversityScore { get; set; } // 0-1, how diverse is their location history

    // Device patterns
    public string CommonDeviceFingerprints { get; set; } = string.Empty; // JSON array
    public string CommonUserAgents { get; set; } = string.Empty; // JSON array
    public int AverageDevicesPerWeek { get; set; }

    // Access patterns
    public string CommonResourcesAccessed { get; set; } = string.Empty; // JSON array of resources
    public string CommonApiEndpoints { get; set; } = string.Empty; // JSON array
    public double AverageApiCallsPerSession { get; set; }
    public double ApiCallFrequencyStdDev { get; set; }
    public string TypicalAccessSequences { get; set; } = string.Empty; // JSON array of common sequences

    // Resource usage patterns
    public double AverageSecretsAccessedPerSession { get; set; }
    public double SecretsAccessStdDev { get; set; }
    public double AverageSessionDurationMinutes { get; set; }
    public double SessionDurationStdDev { get; set; }

    // Privilege patterns
    public bool TypicallyUsesPrivilegedAccess { get; set; }
    public double AveragePrivilegedSessionsPerWeek { get; set; }
    public string CommonPrivilegedResources { get; set; } = string.Empty; // JSON array

    // Anomaly scores (updated real-time)
    public double CurrentAnomalyScore { get; set; } // 0-100
    public double LastSessionAnomalyScore { get; set; }
    public int AnomaliesDetectedLast30Days { get; set; }
    public int FalsePositivesLast30Days { get; set; }

    // ML model info
    public string ModelVersion { get; set; } = string.Empty;
    public DateTime? ModelLastTrained { get; set; }
    public double ModelAccuracy { get; set; }
    public double ModelPrecision { get; set; }
    public double ModelRecall { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual ApplicationUser User { get; set; } = null!;
}
