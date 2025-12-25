namespace USP.Core.Models.DTOs.PAM;

/// <summary>
/// Result of password rotation
/// </summary>
public class PasswordRotationResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime? RotatedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public bool CredentialsVerified { get; set; }
}

/// <summary>
/// Password rotation history entry
/// </summary>
public class PasswordRotationHistoryDto
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public DateTime RotatedAt { get; set; }
    public string RotatedBy { get; set; } = string.Empty; // "manual", "scheduled", "on_checkout", "on_expiration"
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public bool CredentialsVerified { get; set; }
    public Guid? RotatedByUserId { get; set; }
}

/// <summary>
/// Account due for rotation
/// </summary>
public class AccountDueForRotationDto
{
    public Guid AccountId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string SafeName { get; set; } = string.Empty;
    public DateTime? LastRotation { get; set; }
    public DateTime? NextRotation { get; set; }
    public int DaysOverdue { get; set; }
    public string RotationPolicy { get; set; } = string.Empty;
}

/// <summary>
/// Rotation statistics
/// </summary>
public class RotationStatisticsDto
{
    public int TotalAccounts { get; set; }
    public int AccountsDueForRotation { get; set; }
    public int AccountsOverdue { get; set; }
    public int RotationsLast24Hours { get; set; }
    public int RotationsLast7Days { get; set; }
    public int RotationsLast30Days { get; set; }
    public int SuccessfulRotations { get; set; }
    public int FailedRotations { get; set; }
    public double SuccessRate { get; set; }
    public List<RotationByPlatformDto> RotationsByPlatform { get; set; } = new();
    public List<RecentRotationDto> RecentRotations { get; set; } = new();
}

/// <summary>
/// Rotations by platform
/// </summary>
public class RotationByPlatformDto
{
    public string Platform { get; set; } = string.Empty;
    public int Count { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
}

/// <summary>
/// Recent rotation summary
/// </summary>
public class RecentRotationDto
{
    public Guid AccountId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public DateTime RotatedAt { get; set; }
    public bool Success { get; set; }
    public string RotatedBy { get; set; } = string.Empty;
}
