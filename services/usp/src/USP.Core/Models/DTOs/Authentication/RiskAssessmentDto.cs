namespace USP.Core.Models.DTOs.Authentication;

/// <summary>
/// Request for risk assessment
/// </summary>
public class RiskAssessmentRequest
{
    /// <summary>
    /// User ID being assessed
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// IP address of the request
    /// </summary>
    public string IpAddress { get; set; } = string.Empty;

    /// <summary>
    /// User agent string
    /// </summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// Device fingerprint
    /// </summary>
    public string? DeviceFingerprint { get; set; }

    /// <summary>
    /// Geolocation country
    /// </summary>
    public string? Country { get; set; }

    /// <summary>
    /// Geolocation city
    /// </summary>
    public string? City { get; set; }

    /// <summary>
    /// Latitude
    /// </summary>
    public double? Latitude { get; set; }

    /// <summary>
    /// Longitude
    /// </summary>
    public double? Longitude { get; set; }

    /// <summary>
    /// Authentication method being used
    /// </summary>
    public string? AuthenticationMethod { get; set; }

    /// <summary>
    /// Whether MFA was used
    /// </summary>
    public bool MfaUsed { get; set; }

    /// <summary>
    /// Resource being accessed (optional)
    /// </summary>
    public string? ResourceAccessed { get; set; }
}

/// <summary>
/// Response from risk assessment
/// </summary>
public class RiskAssessmentResponse
{
    /// <summary>
    /// Risk level: low, medium, high, critical, unknown
    /// </summary>
    public string RiskLevel { get; set; } = "unknown";

    /// <summary>
    /// Numerical risk score (0-100)
    /// </summary>
    public int RiskScore { get; set; }

    /// <summary>
    /// List of risk factors detected
    /// </summary>
    public List<string> RiskFactors { get; set; } = new();

    /// <summary>
    /// Whether additional verification is required
    /// </summary>
    public bool RequireAdditionalVerification { get; set; }

    /// <summary>
    /// Recommended security actions
    /// </summary>
    public List<string> RecommendedActions { get; set; } = new();

    /// <summary>
    /// Whether impossible travel was detected
    /// </summary>
    public bool ImpossibleTravelDetected { get; set; }

    /// <summary>
    /// Whether the IP is suspicious
    /// </summary>
    public bool SuspiciousIpDetected { get; set; }

    /// <summary>
    /// Velocity check result
    /// </summary>
    public bool VelocityCheckFailed { get; set; }
}
