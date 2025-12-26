namespace USP.Core.Models.Configuration;

/// <summary>
/// Rate limiting configuration settings
/// </summary>
public class RateLimitingSettings
{
    /// <summary>
    /// Enable global rate limiting
    /// </summary>
    public bool EnableGlobalRateLimiting { get; set; } = true;

    /// <summary>
    /// Enable per-user rate limiting
    /// </summary>
    public bool EnablePerUserRateLimiting { get; set; } = true;

    /// <summary>
    /// Enable per-IP rate limiting
    /// </summary>
    public bool EnablePerIpRateLimiting { get; set; } = true;

    /// <summary>
    /// Enable per-endpoint rate limiting
    /// </summary>
    public bool EnablePerEndpointRateLimiting { get; set; } = true;

    /// <summary>
    /// Per-user requests allowed per minute
    /// </summary>
    public int PerUserRequestsPerMinute { get; set; } = 100;

    /// <summary>
    /// Per-user requests allowed per hour
    /// </summary>
    public int PerUserRequestsPerHour { get; set; } = 5000;

    /// <summary>
    /// Per-IP requests allowed per minute
    /// </summary>
    public int PerIpRequestsPerMinute { get; set; } = 200;

    /// <summary>
    /// Per-IP requests allowed per hour
    /// </summary>
    public int PerIpRequestsPerHour { get; set; } = 10000;

    /// <summary>
    /// Global requests allowed per second (DDoS protection)
    /// </summary>
    public int GlobalRequestsPerSecond { get; set; } = 5000;

    /// <summary>
    /// Burst allowance percentage (e.g., 20 means 20% additional requests allowed temporarily)
    /// </summary>
    public int BurstAllowancePercentage { get; set; } = 20;

    /// <summary>
    /// Login endpoint requests per minute (brute force protection)
    /// </summary>
    public int LoginRequestsPerMinute { get; set; } = 10;

    /// <summary>
    /// MFA endpoint requests per minute
    /// </summary>
    public int MfaRequestsPerMinute { get; set; } = 20;

    /// <summary>
    /// Secrets endpoint requests per minute
    /// </summary>
    public int SecretsRequestsPerMinute { get; set; } = 1000;

    /// <summary>
    /// Rate limit violation penalty duration in minutes
    /// </summary>
    public int ViolationPenaltyMinutes { get; set; } = 15;

    /// <summary>
    /// Number of violations before applying penalty
    /// </summary>
    public int ViolationsBeforePenalty { get; set; } = 3;

    /// <summary>
    /// Enable sliding window algorithm (more accurate than fixed window)
    /// </summary>
    public bool UseSlidingWindow { get; set; } = true;

    /// <summary>
    /// Validates rate limiting configuration settings
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when validation fails</exception>
    public void Validate()
    {
        if (PerUserRequestsPerMinute <= 0)
        {
            throw new InvalidOperationException("Per-user requests per minute must be positive");
        }

        if (PerUserRequestsPerHour <= 0)
        {
            throw new InvalidOperationException("Per-user requests per hour must be positive");
        }

        if (PerIpRequestsPerMinute <= 0)
        {
            throw new InvalidOperationException("Per-IP requests per minute must be positive");
        }

        if (PerIpRequestsPerHour <= 0)
        {
            throw new InvalidOperationException("Per-IP requests per hour must be positive");
        }

        if (GlobalRequestsPerSecond <= 0)
        {
            throw new InvalidOperationException("Global requests per second must be positive");
        }

        if (BurstAllowancePercentage < 0 || BurstAllowancePercentage > 100)
        {
            throw new InvalidOperationException("Burst allowance percentage must be between 0 and 100");
        }

        if (LoginRequestsPerMinute <= 0)
        {
            throw new InvalidOperationException("Login requests per minute must be positive");
        }

        if (MfaRequestsPerMinute <= 0)
        {
            throw new InvalidOperationException("MFA requests per minute must be positive");
        }

        if (SecretsRequestsPerMinute <= 0)
        {
            throw new InvalidOperationException("Secrets requests per minute must be positive");
        }

        if (ViolationPenaltyMinutes <= 0)
        {
            throw new InvalidOperationException("Violation penalty minutes must be positive");
        }

        if (ViolationsBeforePenalty <= 0)
        {
            throw new InvalidOperationException("Violations before penalty must be positive");
        }
    }
}
