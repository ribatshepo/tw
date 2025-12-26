using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using USP.Core.Models.DTOs.PAM;
using USP.Core.Services.PAM;
using USP.Infrastructure.Data;

namespace USP.Infrastructure.Services.PAM;

public class AccessAnalyticsEngine : IAccessAnalyticsEngine
{
    private readonly ApplicationDbContext _context;
    private readonly ISafeManagementService _safeService;
    private readonly ILogger<AccessAnalyticsEngine> _logger;

    public AccessAnalyticsEngine(
        ApplicationDbContext context,
        ISafeManagementService safeService,
        ILogger<AccessAnalyticsEngine> logger)
    {
        _context = context;
        _safeService = safeService;
        _logger = logger;
    }

    public async Task<List<DormantAccountDto>> DetectDormantAccountsAsync(Guid userId, int dormantDays = 90)
    {
        try
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-dormantDays);

            // Get all safes accessible by user
            var safes = await _safeService.GetSafesAsync(userId);
            var safeIds = safes.Select(s => s.Id).ToList();

            // Get accounts with their last checkout/rotation dates
            var accounts = await _context.PrivilegedAccounts
                .Include(a => a.Safe)
                .Where(a => safeIds.Contains(a.SafeId))
                .ToListAsync();

            var dormantAccounts = new List<DormantAccountDto>();

            foreach (var account in accounts)
            {
                // Get last checkout
                var lastCheckout = await _context.AccountCheckouts
                    .Where(c => c.AccountId == account.Id)
                    .OrderByDescending(c => c.CheckoutTime)
                    .Select(c => c.CheckoutTime)
                    .FirstOrDefaultAsync();

                // Determine last activity date
                var lastActivity = new[] { lastCheckout, account.LastRotated, account.CreatedAt }
                    .Where(d => d.HasValue || d != default)
                    .Select(d => d ?? account.CreatedAt)
                    .Max();

                if (lastActivity < cutoffDate)
                {
                    var daysSinceLastUse = (int)(DateTime.UtcNow - lastActivity).TotalDays;
                    var riskScore = CalculateDormantRiskScore(daysSinceLastUse);

                    dormantAccounts.Add(new DormantAccountDto
                    {
                        AccountId = account.Id,
                        AccountName = account.AccountName,
                        Platform = account.Platform,
                        SafeName = account.Safe.Name,
                        LastCheckout = lastCheckout,
                        LastRotation = account.LastRotated,
                        DaysSinceLastUse = daysSinceLastUse,
                        RiskScore = riskScore,
                        Recommendation = GetDormantRecommendation(daysSinceLastUse)
                    });
                }
            }

            _logger.LogInformation(
                "Detected {Count} dormant accounts (dormant > {DormantDays} days) for user {UserId}",
                dormantAccounts.Count,
                dormantDays,
                userId);

            return dormantAccounts.OrderByDescending(a => a.DaysSinceLastUse).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting dormant accounts for user {UserId}", userId);
            return new List<DormantAccountDto>();
        }
    }

    public async Task<List<OverPrivilegedAccountDto>> DetectOverPrivilegedAccountsAsync(Guid userId)
    {
        try
        {
            var safes = await _safeService.GetSafesAsync(userId);
            var safeIds = safes.Select(s => s.Id).ToList();

            var accounts = await _context.PrivilegedAccounts
                .Include(a => a.Safe)
                .Where(a => safeIds.Contains(a.SafeId))
                .ToListAsync();

            var overPrivilegedAccounts = new List<OverPrivilegedAccountDto>();

            foreach (var account in accounts)
            {
                var checkoutCount = await _context.AccountCheckouts
                    .CountAsync(c => c.AccountId == account.Id);

                var sessionCount = await _context.PrivilegedSessions
                    .CountAsync(s => s.AccountId == account.Id);

                // Consider account over-privileged if it has high privilege level but low usage
                var isOverPrivileged = IsAccountOverPrivileged(account.Platform, checkoutCount, sessionCount);

                if (isOverPrivileged)
                {
                    var privilegeScore = CalculatePrivilegeScore(account.Platform, checkoutCount, sessionCount);

                    overPrivilegedAccounts.Add(new OverPrivilegedAccountDto
                    {
                        AccountId = account.Id,
                        AccountName = account.AccountName,
                        Platform = account.Platform,
                        SafeName = account.Safe.Name,
                        CheckoutCount = checkoutCount,
                        SessionCount = sessionCount,
                        GrantedPermissions = GetGrantedPermissions(account.Platform, account.Username),
                        UsedPermissions = new List<string>(), // Would need session command analysis
                        UnusedPermissions = new List<string>(), // Would need session command analysis
                        PrivilegeScore = privilegeScore,
                        Recommendation = "Consider reducing privileges or implementing Just-In-Time access"
                    });
                }
            }

            _logger.LogInformation(
                "Detected {Count} over-privileged accounts for user {UserId}",
                overPrivilegedAccounts.Count,
                userId);

            return overPrivilegedAccounts.OrderByDescending(a => a.PrivilegeScore).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting over-privileged accounts for user {UserId}", userId);
            return new List<OverPrivilegedAccountDto>();
        }
    }

    public async Task<AccountUsagePatternDto> AnalyzeAccountUsageAsync(
        Guid accountId,
        Guid userId,
        int daysToAnalyze = 30)
    {
        try
        {
            var account = await _context.PrivilegedAccounts
                .Include(a => a.Safe)
                .FirstOrDefaultAsync(a => a.Id == accountId);

            if (account == null)
                throw new InvalidOperationException("Account not found");

            // Check access
            var hasAccess = await _safeService.HasSafeAccessAsync(account.SafeId, userId, "read");
            if (!hasAccess)
                throw new InvalidOperationException("Access denied");

            var cutoffDate = DateTime.UtcNow.AddDays(-daysToAnalyze);

            var checkouts = await _context.AccountCheckouts
                .Include(c => c.User)
                .Where(c => c.AccountId == accountId && c.CheckoutTime >= cutoffDate)
                .ToListAsync();

            var sessions = await _context.PrivilegedSessions
                .Include(s => s.User)
                .Where(s => s.AccountId == accountId && s.StartTime >= cutoffDate)
                .ToListAsync();

            var totalCommands = await _context.SessionCommands
                .Where(c => sessions.Select(s => s.Id).Contains(c.SessionId))
                .CountAsync();

            var avgSessionDuration = sessions
                .Where(s => s.EndTime.HasValue)
                .Select(s => (s.EndTime!.Value - s.StartTime).TotalMinutes)
                .DefaultIfEmpty(0)
                .Average();

            // Analyze checkout patterns by hour
            var checkoutsByHour = checkouts
                .GroupBy(c => c.CheckoutTime.Hour)
                .ToDictionary(g => g.Key, g => g.Count());

            // Analyze checkout patterns by day of week
            var checkoutsByDayOfWeek = checkouts
                .GroupBy(c => c.CheckoutTime.DayOfWeek)
                .ToDictionary(g => g.Key, g => g.Count());

            // Top users
            var topUsers = checkouts
                .GroupBy(c => new { c.UserId, c.User.Email })
                .Select(g => new TopUserDto
                {
                    UserId = g.Key.UserId,
                    UserEmail = g.Key.Email ?? string.Empty,
                    CheckoutCount = g.Count(),
                    SessionCount = sessions.Count(s => s.UserId == g.Key.UserId)
                })
                .OrderByDescending(u => u.CheckoutCount)
                .Take(5)
                .ToList();

            // Detect anomalous patterns
            var (hasAnomaly, anomalyReason) = DetectUsageAnomaly(checkoutsByHour, checkoutsByDayOfWeek);

            var pattern = new AccountUsagePatternDto
            {
                AccountId = accountId,
                AccountName = account.AccountName,
                TotalCheckouts = checkouts.Count,
                TotalSessions = sessions.Count,
                TotalCommands = totalCommands,
                AverageSessionDuration = TimeSpan.FromMinutes(avgSessionDuration),
                CheckoutsByHour = checkoutsByHour,
                CheckoutsByDayOfWeek = checkoutsByDayOfWeek,
                TopUsers = topUsers,
                MostUsedCommands = new List<string>(), // Would need command frequency analysis
                HasAnomalousPattern = hasAnomaly,
                AnomalyReason = anomalyReason
            };

            _logger.LogInformation(
                "Analyzed usage pattern for account {AccountId} over {Days} days",
                accountId,
                daysToAnalyze);

            return pattern;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing account usage for account {AccountId}", accountId);
            throw;
        }
    }

    public async Task<List<AccessAnomalyDto>> DetectAccessAnomaliesAsync(Guid userId)
    {
        try
        {
            var anomalies = new List<AccessAnomalyDto>();
            var safes = await _safeService.GetSafesAsync(userId);
            var safeIds = safes.Select(s => s.Id).ToList();

            // Detect unusual time access (checkouts outside business hours)
            var recentCheckouts = await _context.AccountCheckouts
                .Include(c => c.Account)
                    .ThenInclude(a => a.Safe)
                .Include(c => c.User)
                .Where(c => safeIds.Contains(c.Account.SafeId) &&
                           c.CheckoutTime >= DateTime.UtcNow.AddDays(-7))
                .ToListAsync();

            foreach (var checkout in recentCheckouts)
            {
                var hour = checkout.CheckoutTime.Hour;
                if (hour < 6 || hour > 22) // Outside 6 AM - 10 PM
                {
                    anomalies.Add(new AccessAnomalyDto
                    {
                        Id = Guid.NewGuid(),
                        AnomalyType = "UnusualTime",
                        AccountId = checkout.AccountId,
                        AccountName = checkout.Account.AccountName,
                        UserId = checkout.UserId,
                        UserEmail = checkout.User.Email,
                        DetectedAt = DateTime.UtcNow,
                        Description = $"Checkout at unusual time: {checkout.CheckoutTime:HH:mm}",
                        Severity = 6,
                        Details = new Dictionary<string, object>
                        {
                            { "checkoutTime", checkout.CheckoutTime },
                            { "hour", hour },
                            { "checkoutId", checkout.Id }
                        },
                        Status = "open"
                    });
                }
            }

            // Detect unusual frequency (multiple checkouts in short period)
            var frequencyAnomalies = recentCheckouts
                .GroupBy(c => new { c.AccountId, c.UserId, Date = c.CheckoutTime.Date })
                .Where(g => g.Count() > 5) // More than 5 checkouts per day
                .ToList();

            foreach (var group in frequencyAnomalies)
            {
                var account = await _context.PrivilegedAccounts.FindAsync(group.Key.AccountId);
                var user = await _context.Users.FindAsync(group.Key.UserId);

                anomalies.Add(new AccessAnomalyDto
                {
                    Id = Guid.NewGuid(),
                    AnomalyType = "UnusualFrequency",
                    AccountId = group.Key.AccountId,
                    AccountName = account?.AccountName ?? string.Empty,
                    UserId = group.Key.UserId,
                    UserEmail = user?.Email,
                    DetectedAt = DateTime.UtcNow,
                    Description = $"Unusually high checkout frequency: {group.Count()} checkouts on {group.Key.Date:yyyy-MM-dd}",
                    Severity = 7,
                    Details = new Dictionary<string, object>
                    {
                        { "checkoutCount", group.Count() },
                        { "date", group.Key.Date }
                    },
                    Status = "open"
                });
            }

            // Detect unusual session duration
            var recentSessions = await _context.PrivilegedSessions
                .Include(s => s.Account)
                    .ThenInclude(a => a.Safe)
                .Include(s => s.User)
                .Where(s => safeIds.Contains(s.Account.SafeId) &&
                           s.EndTime.HasValue &&
                           s.StartTime >= DateTime.UtcNow.AddDays(-7))
                .ToListAsync();

            foreach (var session in recentSessions)
            {
                var duration = session.EndTime!.Value - session.StartTime;
                if (duration.TotalHours > 8) // Session longer than 8 hours
                {
                    anomalies.Add(new AccessAnomalyDto
                    {
                        Id = Guid.NewGuid(),
                        AnomalyType = "UnusualDuration",
                        AccountId = session.AccountId,
                        AccountName = session.Account.AccountName,
                        UserId = session.UserId,
                        UserEmail = session.User.Email,
                        DetectedAt = DateTime.UtcNow,
                        Description = $"Unusually long session duration: {duration.TotalHours:F1} hours",
                        Severity = 5,
                        Details = new Dictionary<string, object>
                        {
                            { "durationHours", duration.TotalHours },
                            { "sessionId", session.Id },
                            { "startTime", session.StartTime },
                            { "endTime", session.EndTime }
                        },
                        Status = "open"
                    });
                }
            }

            _logger.LogInformation(
                "Detected {Count} access anomalies for user {UserId}",
                anomalies.Count,
                userId);

            return anomalies.OrderByDescending(a => a.Severity).ThenByDescending(a => a.DetectedAt).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting access anomalies for user {UserId}", userId);
            return new List<AccessAnomalyDto>();
        }
    }

    public async Task<ComplianceDashboardDto> GetComplianceDashboardAsync(Guid userId)
    {
        try
        {
            var safes = await _safeService.GetSafesAsync(userId);
            var safeIds = safes.Select(s => s.Id).ToList();

            var accounts = await _context.PrivilegedAccounts
                .Where(a => safeIds.Contains(a.SafeId))
                .ToListAsync();

            var totalAccounts = accounts.Count;
            var accountsWithMfa = accounts.Count(a => a.RequiresMfa);
            var accountsWithApproval = accounts.Count(a => a.RequiresDualApproval);

            var dormantAccounts = (await DetectDormantAccountsAsync(userId, 90)).Count;
            var expiredRotation = accounts.Count(a =>
                a.NextRotation.HasValue && a.NextRotation.Value < DateTime.UtcNow);

            var highRiskAccounts = (await GetHighRiskAccountsAsync(userId, 70)).Count;
            var openAnomalies = (await DetectAccessAnomaliesAsync(userId))
                .Count(a => a.Status == "open");

            // Calculate compliance score
            var complianceScore = CalculateComplianceScore(
                totalAccounts,
                accountsWithMfa,
                accountsWithApproval,
                dormantAccounts,
                expiredRotation,
                highRiskAccounts);

            var dashboard = new ComplianceDashboardDto
            {
                TotalPrivilegedAccounts = totalAccounts,
                AccountsWithMfa = accountsWithMfa,
                AccountsWithApprovalRequired = accountsWithApproval,
                DormantAccounts = dormantAccounts,
                AccountsWithExpiredRotation = expiredRotation,
                AccountsWithoutSessionRecording = 0, // All sessions are recorded
                HighRiskAccounts = highRiskAccounts,
                OpenAccessAnomalies = openAnomalies,
                ComplianceScore = complianceScore,
                TopViolations = GetTopViolations(dormantAccounts, expiredRotation, accountsWithMfa, totalAccounts),
                ViolationsByCategory = new Dictionary<string, int>
                {
                    { "DormantAccounts", dormantAccounts },
                    { "ExpiredRotation", expiredRotation },
                    { "MissingMFA", totalAccounts - accountsWithMfa },
                    { "MissingApproval", totalAccounts - accountsWithApproval }
                },
                ComplianceTrends = new List<ComplianceTrendDto>() // Would need historical tracking
            };

            _logger.LogInformation(
                "Generated compliance dashboard for user {UserId}: Score {Score:F1}%",
                userId,
                complianceScore);

            return dashboard;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating compliance dashboard for user {UserId}", userId);
            throw;
        }
    }

    public async Task<AccountRiskScoreDto> CalculateAccountRiskScoreAsync(Guid accountId, Guid userId)
    {
        try
        {
            var account = await _context.PrivilegedAccounts
                .Include(a => a.Safe)
                .FirstOrDefaultAsync(a => a.Id == accountId);

            if (account == null)
                throw new InvalidOperationException("Account not found");

            var hasAccess = await _safeService.HasSafeAccessAsync(account.SafeId, userId, "read");
            if (!hasAccess)
                throw new InvalidOperationException("Access denied");

            var riskFactors = new Dictionary<string, int>();
            var recommendations = new List<string>();

            // Factor 1: Dormancy risk (0-20 points)
            var lastCheckout = await _context.AccountCheckouts
                .Where(c => c.AccountId == accountId)
                .OrderByDescending(c => c.CheckoutTime)
                .Select(c => c.CheckoutTime)
                .FirstOrDefaultAsync();

            var daysSinceLastUse = lastCheckout != default
                ? (int)(DateTime.UtcNow - lastCheckout).TotalDays
                : (int)(DateTime.UtcNow - account.CreatedAt).TotalDays;

            var dormancyRisk = Math.Min(20, daysSinceLastUse / 5);
            riskFactors["Dormancy"] = dormancyRisk;

            if (dormancyRisk > 15)
                recommendations.Add("Account is dormant - consider deactivation or review");

            // Factor 2: Rotation risk (0-25 points)
            var rotationRisk = 0;
            if (account.NextRotation.HasValue && account.NextRotation.Value < DateTime.UtcNow)
            {
                var daysOverdue = (int)(DateTime.UtcNow - account.NextRotation.Value).TotalDays;
                rotationRisk = Math.Min(25, daysOverdue / 2);
                recommendations.Add("Password rotation overdue - rotate immediately");
            }
            riskFactors["RotationOverdue"] = rotationRisk;

            // Factor 3: MFA risk (0-15 points)
            var mfaRisk = account.RequiresMfa ? 0 : 15;
            riskFactors["MissingMFA"] = mfaRisk;

            if (mfaRisk > 0)
                recommendations.Add("Enable MFA for enhanced security");

            // Factor 4: Approval risk (0-15 points)
            var approvalRisk = account.RequiresDualApproval ? 0 : 15;
            riskFactors["MissingApproval"] = approvalRisk;

            if (approvalRisk > 0)
                recommendations.Add("Enable dual approval for privileged access");

            // Factor 5: Platform privilege risk (0-25 points)
            var privilegeRisk = GetPlatformPrivilegeRisk(account.Platform, account.Username);
            riskFactors["PrivilegeLevel"] = privilegeRisk;

            if (privilegeRisk > 15)
                recommendations.Add("High privilege level - implement Just-In-Time access");

            var totalRiskScore = riskFactors.Values.Sum();
            var riskLevel = GetRiskLevel(totalRiskScore);

            var riskScore = new AccountRiskScoreDto
            {
                AccountId = accountId,
                AccountName = account.AccountName,
                Platform = account.Platform,
                SafeName = account.Safe.Name,
                TotalRiskScore = totalRiskScore,
                RiskFactors = riskFactors,
                RiskLevel = riskLevel,
                Recommendations = recommendations,
                CalculatedAt = DateTime.UtcNow
            };

            _logger.LogInformation(
                "Calculated risk score for account {AccountId}: {Score} ({Level})",
                accountId,
                totalRiskScore,
                riskLevel);

            return riskScore;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating risk score for account {AccountId}", accountId);
            throw;
        }
    }

    public async Task<List<AccountRiskScoreDto>> GetHighRiskAccountsAsync(Guid userId, int threshold = 70)
    {
        try
        {
            var safes = await _safeService.GetSafesAsync(userId);
            var safeIds = safes.Select(s => s.Id).ToList();

            var accounts = await _context.PrivilegedAccounts
                .Where(a => safeIds.Contains(a.SafeId))
                .ToListAsync();

            var highRiskAccounts = new List<AccountRiskScoreDto>();

            foreach (var account in accounts)
            {
                var riskScore = await CalculateAccountRiskScoreAsync(account.Id, userId);
                if (riskScore.TotalRiskScore >= threshold)
                {
                    highRiskAccounts.Add(riskScore);
                }
            }

            _logger.LogInformation(
                "Found {Count} high-risk accounts (threshold: {Threshold}) for user {UserId}",
                highRiskAccounts.Count,
                threshold,
                userId);

            return highRiskAccounts.OrderByDescending(a => a.TotalRiskScore).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting high-risk accounts for user {UserId}", userId);
            return new List<AccountRiskScoreDto>();
        }
    }

    public async Task<List<CheckoutPolicyViolationDto>> DetectCheckoutPolicyViolationsAsync(Guid userId)
    {
        try
        {
            var violations = new List<CheckoutPolicyViolationDto>();
            var safes = await _safeService.GetSafesAsync(userId);
            var safeIds = safes.Select(s => s.Id).ToList();

            var recentCheckouts = await _context.AccountCheckouts
                .Include(c => c.Account)
                .Include(c => c.User)
                .Where(c => safeIds.Contains(c.Account.SafeId) &&
                           c.CheckoutTime >= DateTime.UtcNow.AddDays(-30))
                .ToListAsync();

            foreach (var checkout in recentCheckouts)
            {
                // Violation: Checked out too long
                if (checkout.Status == "active" &&
                    (DateTime.UtcNow - checkout.CheckoutTime).TotalHours > 24)
                {
                    violations.Add(new CheckoutPolicyViolationDto
                    {
                        CheckoutId = checkout.Id,
                        AccountId = checkout.AccountId,
                        AccountName = checkout.Account.AccountName,
                        UserId = checkout.UserId,
                        UserEmail = checkout.User.Email ?? string.Empty,
                        CheckoutTime = checkout.CheckoutTime,
                        ViolationType = "ExcessiveDuration",
                        Description = "Checkout duration exceeds 24 hours",
                        Severity = "Medium",
                        AutoTerminated = false
                    });
                }

                // Violation: Missing approval when required
                if (checkout.Account.RequiresDualApproval && !checkout.ApprovalRequired)
                {
                    violations.Add(new CheckoutPolicyViolationDto
                    {
                        CheckoutId = checkout.Id,
                        AccountId = checkout.AccountId,
                        AccountName = checkout.Account.AccountName,
                        UserId = checkout.UserId,
                        UserEmail = checkout.User.Email ?? string.Empty,
                        CheckoutTime = checkout.CheckoutTime,
                        ViolationType = "MissingApproval",
                        Description = "Dual approval required but not enforced",
                        Severity = "High",
                        AutoTerminated = false
                    });
                }
            }

            _logger.LogInformation(
                "Detected {Count} checkout policy violations for user {UserId}",
                violations.Count,
                userId);

            return violations.OrderByDescending(v => v.CheckoutTime).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting checkout policy violations for user {UserId}", userId);
            return new List<CheckoutPolicyViolationDto>();
        }
    }

    public async Task<AccessAnalyticsSummaryDto> GetAnalyticsSummaryAsync(Guid userId)
    {
        try
        {
            var safes = await _safeService.GetSafesAsync(userId);
            var safeIds = safes.Select(s => s.Id).ToList();

            var allAccounts = await _context.PrivilegedAccounts
                .Where(a => safeIds.Contains(a.SafeId))
                .ToListAsync();

            var dormantAccounts = await DetectDormantAccountsAsync(userId, 90);
            var overPrivilegedAccounts = await DetectOverPrivilegedAccountsAsync(userId);
            var highRiskAccounts = await GetHighRiskAccountsAsync(userId, 70);
            var anomalies = await DetectAccessAnomaliesAsync(userId);
            var policyViolations = await DetectCheckoutPolicyViolationsAsync(userId);

            var activeAccounts = allAccounts.Count - dormantAccounts.Count;

            var riskScores = new List<int>();
            foreach (var account in allAccounts.Take(100)) // Limit to avoid performance issues
            {
                var riskScore = await CalculateAccountRiskScoreAsync(account.Id, userId);
                riskScores.Add(riskScore.TotalRiskScore);
            }

            var averageRiskScore = riskScores.Any() ? riskScores.Average() : 0;

            var complianceDashboard = await GetComplianceDashboardAsync(userId);

            var accountsByPlatform = allAccounts
                .GroupBy(a => a.Platform)
                .ToDictionary(g => g.Key, g => g.Count());

            var accountsByRiskLevel = highRiskAccounts
                .GroupBy(a => a.RiskLevel)
                .ToDictionary(g => g.Key, g => g.Count());

            var summary = new AccessAnalyticsSummaryDto
            {
                TotalPrivilegedAccounts = allAccounts.Count,
                ActiveAccounts = activeAccounts,
                DormantAccounts = dormantAccounts.Count,
                OverPrivilegedAccounts = overPrivilegedAccounts.Count,
                HighRiskAccounts = highRiskAccounts.Count,
                OpenAnomalies = anomalies.Count(a => a.Status == "open"),
                PolicyViolationsLast30Days = policyViolations.Count,
                AverageRiskScore = averageRiskScore,
                ComplianceScore = complianceDashboard.ComplianceScore,
                AccountsByPlatform = accountsByPlatform,
                AccountsByRiskLevel = accountsByRiskLevel,
                TopDormantAccounts = dormantAccounts.Take(10).ToList(),
                TopRiskyAccounts = highRiskAccounts.Take(10).ToList(),
                RecentAnomalies = anomalies.Take(10).ToList()
            };

            _logger.LogInformation(
                "Generated analytics summary for user {UserId}",
                userId);

            return summary;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating analytics summary for user {UserId}", userId);
            throw;
        }
    }

    // Helper methods

    private static int CalculateDormantRiskScore(int daysSinceLastUse)
    {
        if (daysSinceLastUse < 30) return 0;
        if (daysSinceLastUse < 60) return 20;
        if (daysSinceLastUse < 90) return 40;
        if (daysSinceLastUse < 180) return 60;
        if (daysSinceLastUse < 365) return 80;
        return 100;
    }

    private static string GetDormantRecommendation(int daysSinceLastUse)
    {
        if (daysSinceLastUse > 180)
            return "Consider deactivating or removing this account";
        if (daysSinceLastUse > 90)
            return "Review account necessity and consider implementing Just-In-Time access";
        return "Monitor account activity and set up alerts for usage";
    }

    private static bool IsAccountOverPrivileged(string platform, int checkoutCount, int sessionCount)
    {
        // Platform-specific privilege detection logic
        var highPrivilegePlatforms = new[] { "PostgreSQL", "MySQL", "SQLServer", "Oracle", "MongoDB", "AWS", "SSH" };

        if (!highPrivilegePlatforms.Contains(platform, StringComparer.OrdinalIgnoreCase))
            return false;

        // If account has low usage, it might be over-privileged
        return checkoutCount < 5 && sessionCount < 10;
    }

    private static int CalculatePrivilegeScore(string platform, int checkoutCount, int sessionCount)
    {
        var baseScore = 50;

        // Higher privilege platforms get higher base score
        if (platform.Equals("AWS", StringComparison.OrdinalIgnoreCase))
            baseScore = 80;
        else if (platform.Equals("Oracle", StringComparison.OrdinalIgnoreCase))
            baseScore = 70;
        else if (platform.StartsWith("SQL", StringComparison.OrdinalIgnoreCase))
            baseScore = 65;

        // Reduce score based on usage
        var usagePenalty = Math.Min(30, (checkoutCount + sessionCount) * 2);
        return Math.Max(0, baseScore - usagePenalty);
    }

    private static List<string> GetGrantedPermissions(string platform, string username)
    {
        // Simplified permission detection - in production, query the actual system
        var permissions = new List<string>();

        if (username.Equals("root", StringComparison.OrdinalIgnoreCase) ||
            username.Equals("admin", StringComparison.OrdinalIgnoreCase) ||
            username.Equals("sa", StringComparison.OrdinalIgnoreCase))
        {
            permissions.Add("FULL_ADMIN");
        }

        return permissions;
    }

    private static (bool hasAnomaly, string? reason) DetectUsageAnomaly(
        Dictionary<int, int> checkoutsByHour,
        Dictionary<DayOfWeek, int> checkoutsByDayOfWeek)
    {
        // Detect unusual patterns (e.g., all checkouts at night, only weekends)
        if (checkoutsByHour.Any())
        {
            var nightCheckouts = checkoutsByHour.Where(kvp => kvp.Key < 6 || kvp.Key > 22).Sum(kvp => kvp.Value);
            var totalCheckouts = checkoutsByHour.Sum(kvp => kvp.Value);

            if (nightCheckouts > totalCheckouts * 0.5)
                return (true, "Majority of checkouts occur during night hours");
        }

        if (checkoutsByDayOfWeek.Any())
        {
            var weekendCheckouts = checkoutsByDayOfWeek
                .Where(kvp => kvp.Key == DayOfWeek.Saturday || kvp.Key == DayOfWeek.Sunday)
                .Sum(kvp => kvp.Value);
            var totalCheckouts = checkoutsByDayOfWeek.Sum(kvp => kvp.Value);

            if (weekendCheckouts > totalCheckouts * 0.7)
                return (true, "Majority of checkouts occur on weekends");
        }

        return (false, null);
    }

    private static int GetPlatformPrivilegeRisk(string platform, string username)
    {
        // High-privilege accounts
        var superUsernames = new[] { "root", "admin", "sa", "postgres", "mysql", "oracle", "system" };

        if (superUsernames.Contains(username, StringComparer.OrdinalIgnoreCase))
            return 25;

        // Platform-specific risk
        return platform switch
        {
            "AWS" => 20,
            "Oracle" => 18,
            "SQLServer" => 15,
            "PostgreSQL" => 15,
            "MySQL" => 15,
            "MongoDB" => 12,
            "Redis" => 10,
            "SSH" => 20,
            _ => 10
        };
    }

    private static string GetRiskLevel(int riskScore)
    {
        return riskScore switch
        {
            >= 80 => "Critical",
            >= 60 => "High",
            >= 40 => "Medium",
            _ => "Low"
        };
    }

    private static double CalculateComplianceScore(
        int totalAccounts,
        int accountsWithMfa,
        int accountsWithApproval,
        int dormantAccounts,
        int expiredRotation,
        int highRiskAccounts)
    {
        if (totalAccounts == 0)
            return 100;

        var score = 100.0;

        // Deduct for missing MFA
        score -= (totalAccounts - accountsWithMfa) * 30.0 / totalAccounts;

        // Deduct for missing approval
        score -= (totalAccounts - accountsWithApproval) * 20.0 / totalAccounts;

        // Deduct for dormant accounts
        score -= dormantAccounts * 15.0 / totalAccounts;

        // Deduct for expired rotation
        score -= expiredRotation * 20.0 / totalAccounts;

        // Deduct for high-risk accounts
        score -= highRiskAccounts * 15.0 / totalAccounts;

        return Math.Max(0, Math.Min(100, score));
    }

    private static List<ComplianceViolationDto> GetTopViolations(
        int dormantAccounts,
        int expiredRotation,
        int accountsWithMfa,
        int totalAccounts)
    {
        var violations = new List<ComplianceViolationDto>();

        if (expiredRotation > 0)
        {
            violations.Add(new ComplianceViolationDto
            {
                ViolationType = "ExpiredPasswordRotation",
                Description = "Privileged accounts with expired password rotation policy",
                Count = expiredRotation,
                Severity = "High",
                Recommendation = "Rotate passwords immediately to maintain security"
            });
        }

        if (dormantAccounts > 0)
        {
            violations.Add(new ComplianceViolationDto
            {
                ViolationType = "DormantAccounts",
                Description = "Privileged accounts that have not been used in 90+ days",
                Count = dormantAccounts,
                Severity = "Medium",
                Recommendation = "Review and deactivate unused privileged accounts"
            });
        }

        var missingMfa = totalAccounts - accountsWithMfa;
        if (missingMfa > 0)
        {
            violations.Add(new ComplianceViolationDto
            {
                ViolationType = "MissingMFA",
                Description = "Privileged accounts without MFA protection",
                Count = missingMfa,
                Severity = "High",
                Recommendation = "Enable MFA for all privileged accounts"
            });
        }

        return violations.OrderByDescending(v => v.Count).ToList();
    }
}
