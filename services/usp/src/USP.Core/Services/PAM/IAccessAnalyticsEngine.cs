using USP.Core.Models.DTOs.PAM;

namespace USP.Core.Services.PAM;

/// <summary>
/// Service for analyzing privileged access patterns and detecting security risks
/// </summary>
public interface IAccessAnalyticsEngine
{
    /// <summary>
    /// Detect dormant privileged accounts (not used in specified days)
    /// </summary>
    Task<List<DormantAccountDto>> DetectDormantAccountsAsync(Guid userId, int dormantDays = 90);

    /// <summary>
    /// Detect over-privileged accounts (accounts with excessive permissions)
    /// </summary>
    Task<List<OverPrivilegedAccountDto>> DetectOverPrivilegedAccountsAsync(Guid userId);

    /// <summary>
    /// Analyze usage patterns for an account
    /// </summary>
    Task<AccountUsagePatternDto> AnalyzeAccountUsageAsync(Guid accountId, Guid userId, int daysToAnalyze = 30);

    /// <summary>
    /// Detect anomalous access patterns
    /// </summary>
    Task<List<AccessAnomalyDto>> DetectAccessAnomaliesAsync(Guid userId);

    /// <summary>
    /// Get compliance dashboard data
    /// </summary>
    Task<ComplianceDashboardDto> GetComplianceDashboardAsync(Guid userId);

    /// <summary>
    /// Calculate risk score for a privileged account
    /// </summary>
    Task<AccountRiskScoreDto> CalculateAccountRiskScoreAsync(Guid accountId, Guid userId);

    /// <summary>
    /// Get accounts with high risk scores
    /// </summary>
    Task<List<AccountRiskScoreDto>> GetHighRiskAccountsAsync(Guid userId, int threshold = 70);

    /// <summary>
    /// Analyze checkout patterns for potential policy violations
    /// </summary>
    Task<List<CheckoutPolicyViolationDto>> DetectCheckoutPolicyViolationsAsync(Guid userId);

    /// <summary>
    /// Get access analytics summary
    /// </summary>
    Task<AccessAnalyticsSummaryDto> GetAnalyticsSummaryAsync(Guid userId);
}
