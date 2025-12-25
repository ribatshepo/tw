namespace USP.Core.Models.DTOs.PAM;

/// <summary>
/// Request to checkout a privileged account
/// </summary>
public class CheckoutRequestDto
{
    public string Reason { get; set; } = string.Empty;
    public int? DurationMinutes { get; set; }
    public bool RotateOnCheckin { get; set; } = false;
}

/// <summary>
/// Response after successful checkout
/// </summary>
public class CheckoutResponseDto
{
    public Guid CheckoutId { get; set; }
    public Guid AccountId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? ConnectionString { get; set; }
    public DateTime CheckedOutAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool RotateOnCheckin { get; set; }
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Request to checkin a privileged account
/// </summary>
public class CheckinRequestDto
{
    public string? Notes { get; set; }
}

/// <summary>
/// Account checkout information
/// </summary>
public class AccountCheckoutDto
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public string UserEmail { get; set; } = string.Empty;
    public DateTime CheckedOutAt { get; set; }
    public DateTime? CheckedInAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty; // active, checkedin, expired, force_checkedin
    public bool RotateOnCheckin { get; set; }
    public Guid? ApprovalId { get; set; }
    public int DurationMinutes { get; set; }
    public bool IsExpired => DateTime.UtcNow > ExpiresAt && Status == "active";
    public int RemainingMinutes => Status == "active" ? Math.Max(0, (int)(ExpiresAt - DateTime.UtcNow).TotalMinutes) : 0;
}

/// <summary>
/// Checkout statistics
/// </summary>
public class CheckoutStatisticsDto
{
    public int ActiveCheckouts { get; set; }
    public int TotalCheckouts { get; set; }
    public int CheckoutsLast24Hours { get; set; }
    public int CheckoutsLast7Days { get; set; }
    public int CheckoutsLast30Days { get; set; }
    public List<AccountCheckoutSummaryDto> RecentCheckouts { get; set; } = new();
    public List<CheckoutByPlatformDto> CheckoutsByPlatform { get; set; } = new();
}

/// <summary>
/// Account checkout summary
/// </summary>
public class AccountCheckoutSummaryDto
{
    public Guid CheckoutId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public DateTime CheckedOutAt { get; set; }
    public DateTime? CheckedInAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
}

/// <summary>
/// Checkout statistics by platform
/// </summary>
public class CheckoutByPlatformDto
{
    public string Platform { get; set; } = string.Empty;
    public int Count { get; set; }
}
