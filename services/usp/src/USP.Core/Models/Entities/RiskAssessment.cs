namespace USP.Core.Models.Entities;

/// <summary>
/// Risk assessment record for audit trail
/// </summary>
public class RiskAssessment
{
    /// <summary>
    /// Assessment ID
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// User ID being assessed
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// IP address
    /// </summary>
    public string IpAddress { get; set; } = string.Empty;

    /// <summary>
    /// User agent
    /// </summary>
    public string UserAgent { get; set; } = string.Empty;

    /// <summary>
    /// Device fingerprint
    /// </summary>
    public string? DeviceFingerprint { get; set; }

    /// <summary>
    /// Risk level determined
    /// </summary>
    public string RiskLevel { get; set; } = "unknown";

    /// <summary>
    /// Numerical risk score (0-100)
    /// </summary>
    public int RiskScore { get; set; }

    /// <summary>
    /// Risk factors detected (JSON array)
    /// </summary>
    public List<string> RiskFactors { get; set; } = new();

    /// <summary>
    /// Action taken (allowed, denied, mfa_required, etc.)
    /// </summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// Assessment timestamp
    /// </summary>
    public DateTime AssessedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Navigation property
    /// </summary>
    public virtual ApplicationUser User { get; set; } = null!;
}
