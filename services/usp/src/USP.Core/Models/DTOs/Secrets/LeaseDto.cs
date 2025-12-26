namespace USP.Core.Models.DTOs.Secrets;

/// <summary>
/// Lease information
/// </summary>
public class LeaseDto
{
    public Guid LeaseId { get; set; }
    public Guid SecretId { get; set; }
    public string SecretPath { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public string UserEmail { get; set; } = string.Empty;
    public DateTime IssuedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public int RenewalCount { get; set; }
    public bool AutoRenewalEnabled { get; set; }
    public string Status { get; set; } = string.Empty;
    public int? MaxRenewals { get; set; }
    public DateTime? LastRenewedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? RevocationReason { get; set; }
    public int LeaseDurationSeconds { get; set; }
    public int RemainingSeconds { get; set; }
    public bool IsExpired { get; set; }
    public bool CanRenew { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
}

/// <summary>
/// Request to create a new lease
/// </summary>
public class CreateLeaseDto
{
    public Guid SecretId { get; set; }
    public int LeaseDurationSeconds { get; set; } = 3600; // 1 hour default
    public bool AutoRenewalEnabled { get; set; } = false;
    public int? MaxRenewals { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
}

/// <summary>
/// Request to renew a lease
/// </summary>
public class RenewLeaseDto
{
    public Guid LeaseId { get; set; }
    public int? IncrementSeconds { get; set; } // null = use original duration
}

/// <summary>
/// Request to revoke a lease
/// </summary>
public class RevokeLeaseDto
{
    public Guid LeaseId { get; set; }
    public string? Reason { get; set; }
}

/// <summary>
/// Lease renewal history entry
/// </summary>
public class LeaseRenewalHistoryDto
{
    public Guid Id { get; set; }
    public Guid LeaseId { get; set; }
    public DateTime RenewedAt { get; set; }
    public DateTime PreviousExpiresAt { get; set; }
    public DateTime NewExpiresAt { get; set; }
    public int RenewalCount { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public Guid? RenewedBy { get; set; }
    public string? RenewedByEmail { get; set; }
    public bool IsAutoRenewal { get; set; }
}

/// <summary>
/// Lease statistics
/// </summary>
public class LeaseStatisticsDto
{
    public int TotalLeases { get; set; }
    public int ActiveLeases { get; set; }
    public int ExpiredLeases { get; set; }
    public int RevokedLeases { get; set; }
    public int AutoRenewalEnabledCount { get; set; }
    public int TotalRenewals { get; set; }
    public double AverageLeaseDurationSeconds { get; set; }
    public double AverageRenewalCount { get; set; }
    public DateTime? OldestActiveLease { get; set; }
    public DateTime? NewestLease { get; set; }
    public int LeasesExpiringIn24Hours { get; set; }
    public int LeasesExpiringIn1Hour { get; set; }
    public Dictionary<string, int> LeasesByStatus { get; set; } = new();
}

/// <summary>
/// Lease configuration settings
/// </summary>
public class LeaseConfigurationDto
{
    public int DefaultLeaseDurationSeconds { get; set; } = 3600;
    public int MaxLeaseDurationSeconds { get; set; } = 86400; // 24 hours
    public int MinLeaseDurationSeconds { get; set; } = 300; // 5 minutes
    public int DefaultMaxRenewals { get; set; } = 10;
    public bool AutoRenewalEnabled { get; set; } = false;
    public int AutoRenewalThresholdSeconds { get; set; } = 600; // Renew 10 minutes before expiration
}
