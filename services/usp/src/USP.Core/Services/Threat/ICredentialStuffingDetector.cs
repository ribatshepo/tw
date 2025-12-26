namespace USP.Core.Services.Threat;

/// <summary>
/// Credential stuffing attack detection service
/// </summary>
public interface ICredentialStuffingDetector
{
    /// <summary>
    /// Detect credential stuffing attempts from IP address
    /// </summary>
    Task<CredentialStuffingDetection> DetectFromIpAsync(string ipAddress);

    /// <summary>
    /// Detect credential stuffing attempts for user
    /// </summary>
    Task<CredentialStuffingDetection> DetectForUserAsync(Guid userId);

    /// <summary>
    /// Check if password is in breached password database
    /// </summary>
    Task<bool> IsPasswordBreachedAsync(string password);

    /// <summary>
    /// Record failed login attempt
    /// </summary>
    Task RecordLoginAttemptAsync(string ipAddress, Guid? userId, bool success);
}

public class CredentialStuffingDetection
{
    public bool IsCredentialStuffing { get; set; }
    public int Confidence { get; set; } // 0-100
    public List<string> Indicators { get; set; } = new();
    public int LoginAttemptsLast5Min { get; set; }
    public int UniqueUsernamesAttempted { get; set; }
    public bool RecommendIpBlock { get; set; }
    public bool RecommendCaptcha { get; set; }
}
