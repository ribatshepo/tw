namespace USP.Core.Services.Threat;

/// <summary>
/// Account takeover detection service with behavioral analysis
/// </summary>
public interface IAccountTakeoverDetector
{
    /// <summary>
    /// Detect account takeover indicators for a user
    /// </summary>
    Task<AccountTakeoverDetection> DetectAsync(Guid userId, DetectionContext context);

    /// <summary>
    /// Analyze device fingerprint changes
    /// </summary>
    Task<DeviceFingerprintAnalysis> AnalyzeDeviceFingerprintAsync(Guid userId, string deviceFingerprint);

    /// <summary>
    /// Analyze behavioral patterns
    /// </summary>
    Task<BehavioralAnalysis> AnalyzeBehavioralPatternsAsync(Guid userId, BehavioralData data);

    /// <summary>
    /// Check for suspicious password changes
    /// </summary>
    Task<bool> IsSuspiciousPasswordChangeAsync(Guid userId);

    /// <summary>
    /// Check for suspicious email changes
    /// </summary>
    Task<bool> IsSuspiciousEmailChangeAsync(Guid userId);

    /// <summary>
    /// Check for suspicious MFA changes
    /// </summary>
    Task<bool> IsSuspiciousMfaChangeAsync(Guid userId);

    /// <summary>
    /// Detect brute force attacks
    /// </summary>
    Task<bool> IsBruteForceAttackAsync(Guid userId);

    /// <summary>
    /// Record successful authentication for behavioral baseline
    /// </summary>
    Task RecordAuthenticationAsync(Guid userId, AuthenticationEvent authEvent);

    /// <summary>
    /// Lock account due to takeover detection
    /// </summary>
    Task LockAccountAsync(Guid userId, string reason);
}

/// <summary>
/// Detection context for account takeover analysis
/// </summary>
public class DetectionContext
{
    public string IpAddress { get; set; } = string.Empty;
    public string UserAgent { get; set; } = string.Empty;
    public string? DeviceFingerprint { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? Country { get; set; }
    public string? City { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Account takeover detection result
/// </summary>
public class AccountTakeoverDetection
{
    public bool IsTakeover { get; set; }
    public int Confidence { get; set; } // 0-100
    public List<string> Indicators { get; set; } = new();
    public string RiskLevel { get; set; } = "low"; // low, medium, high, critical
    public bool RecommendLock { get; set; }
    public bool RecommendMfaChallenge { get; set; }
    public bool RecommendNotification { get; set; }
    public Dictionary<string, object> AdditionalData { get; set; } = new();
}

/// <summary>
/// Device fingerprint analysis result
/// </summary>
public class DeviceFingerprintAnalysis
{
    public bool IsNewDevice { get; set; }
    public bool IsSuspicious { get; set; }
    public int DeviceCount { get; set; }
    public DateTime? LastSeenWithDevice { get; set; }
    public List<string> Anomalies { get; set; } = new();
}

/// <summary>
/// Behavioral analysis result
/// </summary>
public class BehavioralAnalysis
{
    public bool IsAnomalous { get; set; }
    public int AnomalyScore { get; set; } // 0-100
    public List<string> Deviations { get; set; } = new();
    public Dictionary<string, double> BehavioralMetrics { get; set; } = new();
}

/// <summary>
/// Behavioral data for analysis
/// </summary>
public class BehavioralData
{
    public int TypingSpeed { get; set; } // Characters per minute
    public int MouseMovements { get; set; }
    public TimeSpan SessionDuration { get; set; }
    public List<string> ResourcesAccessed { get; set; } = new();
    public int ApiCallCount { get; set; }
    public Dictionary<string, int> ActionFrequency { get; set; } = new();
}

/// <summary>
/// Authentication event for behavioral baseline
/// </summary>
public class AuthenticationEvent
{
    public string IpAddress { get; set; } = string.Empty;
    public string UserAgent { get; set; } = string.Empty;
    public string? DeviceFingerprint { get; set; }
    public string? Country { get; set; }
    public string? City { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public bool MfaUsed { get; set; }
    public string AuthMethod { get; set; } = "password"; // password, webauthn, oauth, etc.
}
