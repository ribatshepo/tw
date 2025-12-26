namespace USP.Core.Models.DTOs.PAM;

public class DormantAccountDto
{
    public Guid AccountId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string SafeName { get; set; } = string.Empty;
    public DateTime? LastCheckout { get; set; }
    public DateTime? LastRotation { get; set; }
    public int DaysSinceLastUse { get; set; }
    public int RiskScore { get; set; }
    public string Recommendation { get; set; } = string.Empty;
}

public class OverPrivilegedAccountDto
{
    public Guid AccountId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string SafeName { get; set; } = string.Empty;
    public int CheckoutCount { get; set; }
    public int SessionCount { get; set; }
    public List<string> GrantedPermissions { get; set; } = new();
    public List<string> UsedPermissions { get; set; } = new();
    public List<string> UnusedPermissions { get; set; } = new();
    public int PrivilegeScore { get; set; }
    public string Recommendation { get; set; } = string.Empty;
}

public class AccountUsagePatternDto
{
    public Guid AccountId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public int TotalCheckouts { get; set; }
    public int TotalSessions { get; set; }
    public int TotalCommands { get; set; }
    public TimeSpan AverageSessionDuration { get; set; }
    public Dictionary<int, int> CheckoutsByHour { get; set; } = new(); // Hour (0-23) -> Count
    public Dictionary<DayOfWeek, int> CheckoutsByDayOfWeek { get; set; } = new();
    public List<TopUserDto> TopUsers { get; set; } = new();
    public List<string> MostUsedCommands { get; set; } = new();
    public bool HasAnomalousPattern { get; set; }
    public string? AnomalyReason { get; set; }
}

public class TopUserDto
{
    public Guid UserId { get; set; }
    public string UserEmail { get; set; } = string.Empty;
    public int CheckoutCount { get; set; }
    public int SessionCount { get; set; }
}

public class AccessAnomalyDto
{
    public Guid Id { get; set; }
    public string AnomalyType { get; set; } = string.Empty; // "UnusualTime", "UnusualLocation", "UnusualFrequency", "UnusualDuration"
    public Guid AccountId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public Guid? UserId { get; set; }
    public string? UserEmail { get; set; }
    public DateTime DetectedAt { get; set; }
    public string Description { get; set; } = string.Empty;
    public int Severity { get; set; } // 1-10
    public Dictionary<string, object> Details { get; set; } = new();
    public string Status { get; set; } = string.Empty; // "open", "investigating", "resolved", "false_positive"
}

public class ComplianceDashboardDto
{
    public int TotalPrivilegedAccounts { get; set; }
    public int AccountsWithMfa { get; set; }
    public int AccountsWithApprovalRequired { get; set; }
    public int DormantAccounts { get; set; }
    public int AccountsWithExpiredRotation { get; set; }
    public int AccountsWithoutSessionRecording { get; set; }
    public int HighRiskAccounts { get; set; }
    public int OpenAccessAnomalies { get; set; }
    public double ComplianceScore { get; set; } // 0-100
    public List<ComplianceViolationDto> TopViolations { get; set; } = new();
    public Dictionary<string, int> ViolationsByCategory { get; set; } = new();
    public List<ComplianceTrendDto> ComplianceTrends { get; set; } = new();
}

public class ComplianceViolationDto
{
    public string ViolationType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Count { get; set; }
    public string Severity { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
}

public class ComplianceTrendDto
{
    public DateTime Date { get; set; }
    public double ComplianceScore { get; set; }
    public int TotalAccounts { get; set; }
    public int CompliantAccounts { get; set; }
}

public class AccountRiskScoreDto
{
    public Guid AccountId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string SafeName { get; set; } = string.Empty;
    public int TotalRiskScore { get; set; } // 0-100
    public Dictionary<string, int> RiskFactors { get; set; } = new();
    public string RiskLevel { get; set; } = string.Empty; // "Low", "Medium", "High", "Critical"
    public List<string> Recommendations { get; set; } = new();
    public DateTime CalculatedAt { get; set; }
}

public class CheckoutPolicyViolationDto
{
    public Guid CheckoutId { get; set; }
    public Guid AccountId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public string UserEmail { get; set; } = string.Empty;
    public DateTime CheckoutTime { get; set; }
    public string ViolationType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public bool AutoTerminated { get; set; }
}

public class AccessAnalyticsSummaryDto
{
    public int TotalPrivilegedAccounts { get; set; }
    public int ActiveAccounts { get; set; }
    public int DormantAccounts { get; set; }
    public int OverPrivilegedAccounts { get; set; }
    public int HighRiskAccounts { get; set; }
    public int OpenAnomalies { get; set; }
    public int PolicyViolationsLast30Days { get; set; }
    public double AverageRiskScore { get; set; }
    public double ComplianceScore { get; set; }
    public Dictionary<string, int> AccountsByPlatform { get; set; } = new();
    public Dictionary<string, int> AccountsByRiskLevel { get; set; } = new();
    public List<DormantAccountDto> TopDormantAccounts { get; set; } = new();
    public List<AccountRiskScoreDto> TopRiskyAccounts { get; set; } = new();
    public List<AccessAnomalyDto> RecentAnomalies { get; set; } = new();
}
