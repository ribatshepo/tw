using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using USP.Core.Models.DTOs.Compliance;
using USP.Core.Services.Compliance;
using USP.Infrastructure.Data;

namespace USP.Infrastructure.Services.Compliance;

/// <summary>
/// HIPAA Security Rule compliance service implementation
/// Ensures compliance with Protected Health Information (PHI) security requirements
/// </summary>
public class HipaaComplianceService : IHipaaComplianceService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<HipaaComplianceService> _logger;

    public HipaaComplianceService(
        ApplicationDbContext context,
        ILogger<HipaaComplianceService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ControlAssessmentResult> Assess164_308_a3_Async(DateTime startDate, DateTime endDate)
    {
        _logger.LogInformation("Assessing HIPAA §164.308(a)(3) - Workforce security");

        var metrics = new Dictionary<string, object>();

        // Check for background check tracking via UserClearance table
        var totalUsers = await _context.Users.CountAsync();
        var usersWithClearance = await _context.UserClearances
            .Where(c => c.ClearanceType == "HipaaWorkforce" &&
                        c.Status == "Approved" &&
                        (c.ExpiresAt == null || c.ExpiresAt > DateTime.UtcNow))
            .Select(c => c.UserId)
            .Distinct()
            .CountAsync();

        metrics["total_users"] = totalUsers;
        metrics["users_with_clearance"] = usersWithClearance;
        metrics["clearance_rate"] = totalUsers > 0 ? (double)usersWithClearance / totalUsers * 100 : 0;

        // Check termination procedures (users deactivated properly)
        var deactivatedUsers = await _context.Users
            .Where(u => !u.IsActive)
            .CountAsync();

        var deactivatedWithAccessRemoval = await _context.Users
            .Where(u => !u.IsActive)
            .Where(u => !_context.UserRoles.Any(ur => ur.UserId == u.Id))
            .CountAsync();

        metrics["deactivated_users"] = deactivatedUsers;
        metrics["deactivated_with_access_removed"] = deactivatedWithAccessRemoval;
        metrics["termination_compliance"] = deactivatedUsers > 0 ? (double)deactivatedWithAccessRemoval / deactivatedUsers * 100 : 100;

        // Check for unauthorized access attempts
        var unauthorizedAttempts = await _context.AuditLogs
            .Where(a => a.Action.Contains("Unauthorized") && a.CreatedAt >= startDate && a.CreatedAt <= endDate)
            .CountAsync();

        metrics["unauthorized_attempts"] = unauthorizedAttempts;

        // Calculate compliance score
        var clearanceScore = totalUsers > 0 ? (double)usersWithClearance / totalUsers * 100 : 100;
        var terminationScore = deactivatedUsers > 0 ? (double)deactivatedWithAccessRemoval / deactivatedUsers * 100 : 100;
        var securityScore = unauthorizedAttempts < 10 ? 100.0 : Math.Max(0, 100 - (unauthorizedAttempts - 10) * 5);

        var overallScore = (clearanceScore + terminationScore + securityScore) / 3;

        var status = overallScore >= 90 ? "implemented" : overallScore >= 70 ? "partial" : "not_implemented";

        var evidence = $"Workforce clearance: {clearanceScore:F2}%, Termination compliance: {terminationScore:F2}%, Unauthorized attempts: {unauthorizedAttempts}";
        var implementation = status == "implemented"
            ? "Workforce security procedures are implemented including authorization/supervision, workforce clearance, and termination procedures."
            : "Workforce security procedures need enhancement.";

        var gaps = status != "implemented"
            ? $"Ensure all {totalUsers - usersWithClearance} users have proper clearance. Improve termination procedures. Address {unauthorizedAttempts} unauthorized access attempts."
            : null;

        return new ControlAssessmentResult
        {
            ControlId = "HIPAA-164.308(a)(3)",
            ControlName = "Workforce Security",
            Status = status,
            Score = overallScore,
            Implementation = implementation,
            Evidence = evidence,
            Gaps = gaps,
            Metrics = metrics
        };
    }

    public async Task<ControlAssessmentResult> Assess164_308_a4_Async(DateTime startDate, DateTime endDate)
    {
        _logger.LogInformation("Assessing HIPAA §164.308(a)(4) - Information access management");

        var metrics = new Dictionary<string, object>();

        // Check role-based access control for PHI
        var totalUsers = await _context.Users.CountAsync();
        var usersWithRoles = await _context.UserRoles
            .Select(ur => ur.UserId)
            .Distinct()
            .CountAsync();

        metrics["total_users"] = totalUsers;
        metrics["users_with_roles"] = usersWithRoles;
        metrics["rbac_coverage"] = totalUsers > 0 ? (double)usersWithRoles / totalUsers * 100 : 0;

        // Check access authorization tracking
        var accessAuthorizations = await _context.AuditLogs
            .Where(a => a.Action.Contains("Authorized") && a.CreatedAt >= startDate && a.CreatedAt <= endDate)
            .CountAsync();

        metrics["access_authorizations"] = accessAuthorizations;

        // Check for access reviews (JIT access as proxy)
        var jitAccessGrants = await _context.JitAccesses
            .Where(j => j.GrantedAt >= startDate && j.GrantedAt <= endDate)
            .CountAsync();

        var jitAccessWithApproval = await _context.JitAccesses
            .Where(j => j.GrantedAt >= startDate && j.GrantedAt <= endDate && j.Status == "approved")
            .CountAsync();

        metrics["jit_access_grants"] = jitAccessGrants;
        metrics["jit_with_approval"] = jitAccessWithApproval;
        metrics["approval_rate"] = jitAccessGrants > 0 ? (double)jitAccessWithApproval / jitAccessGrants * 100 : 100;

        // Check access policy enforcement
        var activePolicies = await _context.AccessPolicies
            .Where(p => p.IsActive && p.PolicyType == "ABAC")
            .CountAsync();

        metrics["active_access_policies"] = activePolicies;

        // Calculate compliance score
        var rbacScore = totalUsers > 0 ? (double)usersWithRoles / totalUsers * 100 : 0;
        var approvalScore = jitAccessGrants > 0 ? (double)jitAccessWithApproval / jitAccessGrants * 100 : 100;
        var policyScore = activePolicies >= 5 ? 100.0 : activePolicies * 20;

        var overallScore = (rbacScore + approvalScore + policyScore) / 3;

        var status = overallScore >= 90 ? "implemented" : overallScore >= 70 ? "partial" : "not_implemented";

        var evidence = $"RBAC coverage: {rbacScore:F2}%, Access approvals: {approvalScore:F2}%, Active policies: {activePolicies}";
        var implementation = status == "implemented"
            ? "Information access management is implemented with role-based access control, access authorization workflows, and policy enforcement."
            : "Information access management needs improvement.";

        var gaps = status != "implemented"
            ? $"Assign roles to {totalUsers - usersWithRoles} users. Implement {5 - activePolicies} additional access policies. Ensure all access is authorized."
            : null;

        return new ControlAssessmentResult
        {
            ControlId = "HIPAA-164.308(a)(4)",
            ControlName = "Information Access Management",
            Status = status,
            Score = overallScore,
            Implementation = implementation,
            Evidence = evidence,
            Gaps = gaps,
            Metrics = metrics
        };
    }

    public async Task<ControlAssessmentResult> Assess164_312_a1_Async(DateTime startDate, DateTime endDate)
    {
        _logger.LogInformation("Assessing HIPAA §164.312(a)(1) - Unique user identification");

        var metrics = new Dictionary<string, object>();

        // Check unique user identifiers
        var totalUsers = await _context.Users.CountAsync();
        var uniqueUsernames = await _context.Users
            .Select(u => u.UserName)
            .Distinct()
            .CountAsync();

        metrics["total_users"] = totalUsers;
        metrics["unique_usernames"] = uniqueUsernames;
        metrics["uniqueness_compliance"] = totalUsers > 0 ? (double)uniqueUsernames / totalUsers * 100 : 100;

        // Check for shared accounts (should be zero)
        var sharedAccounts = totalUsers - uniqueUsernames;
        metrics["shared_accounts"] = sharedAccounts;

        // Check authentication tracking
        var loginEvents = await _context.AuditLogs
            .Where(a => (a.Action == "LoginSuccess" || a.Action == "LoginFailed") &&
                        a.CreatedAt >= startDate && a.CreatedAt <= endDate)
            .CountAsync();

        var uniqueLoginUsers = await _context.AuditLogs
            .Where(a => (a.Action == "LoginSuccess" || a.Action == "LoginFailed") &&
                        a.CreatedAt >= startDate && a.CreatedAt <= endDate &&
                        a.UserId != null)
            .Select(a => a.UserId)
            .Distinct()
            .CountAsync();

        metrics["login_events"] = loginEvents;
        metrics["unique_login_users"] = uniqueLoginUsers;

        // Check for service accounts (should be clearly identified)
        var serviceAccountPattern = "service_";
        var serviceAccounts = await _context.Users
            .Where(u => u.UserName.StartsWith(serviceAccountPattern))
            .CountAsync();

        metrics["service_accounts"] = serviceAccounts;

        // Calculate compliance score
        var uniquenessScore = totalUsers > 0 ? (double)uniqueUsernames / totalUsers * 100 : 100;
        var sharedAccountScore = sharedAccounts == 0 ? 100.0 : Math.Max(0, 100 - (sharedAccounts * 20));
        var trackingScore = loginEvents > 0 ? 100.0 : 70.0;

        var overallScore = (uniquenessScore + sharedAccountScore + trackingScore) / 3;

        var status = uniquenessScore == 100 && sharedAccounts == 0 ? "implemented" : uniquenessScore >= 95 ? "partial" : "not_implemented";

        var evidence = $"Unique identifiers: {uniqueUsernames}/{totalUsers}, Shared accounts: {sharedAccounts}, Login tracking: {loginEvents} events";
        var implementation = status == "implemented"
            ? "Unique user identification is implemented. Each user has a unique identifier, and all access is tracked to specific users."
            : "Unique user identification needs improvement.";

        var gaps = status != "implemented"
            ? $"Eliminate {sharedAccounts} shared accounts. Ensure all users have unique identifiers."
            : null;

        return new ControlAssessmentResult
        {
            ControlId = "HIPAA-164.312(a)(1)",
            ControlName = "Unique User Identification",
            Status = status,
            Score = overallScore,
            Implementation = implementation,
            Evidence = evidence,
            Gaps = gaps,
            Metrics = metrics
        };
    }

    public async Task<ControlAssessmentResult> Assess164_312_a2i_Async(DateTime startDate, DateTime endDate)
    {
        _logger.LogInformation("Assessing HIPAA §164.312(a)(2)(i) - Emergency access procedure");

        var metrics = new Dictionary<string, object>();

        // Check break-glass access implementation
        var breakGlassPolicies = await _context.BreakGlassPolicies.CountAsync();
        var breakGlassAccesses = await _context.BreakGlassAccesses
            .Where(b => b.ActivatedAt >= startDate && b.ActivatedAt <= endDate)
            .CountAsync();

        metrics["break_glass_policies"] = breakGlassPolicies;
        metrics["break_glass_activations"] = breakGlassAccesses;

        // Check emergency access with audit trail
        var emergencyAccessWithAudit = await _context.BreakGlassAccesses
            .Where(b => b.ActivatedAt >= startDate && b.ActivatedAt <= endDate &&
                        !string.IsNullOrEmpty(b.Justification))
            .CountAsync();

        metrics["emergency_access_with_audit"] = emergencyAccessWithAudit;
        metrics["audit_compliance"] = breakGlassAccesses > 0 ? (double)emergencyAccessWithAudit / breakGlassAccesses * 100 : 100;

        // Check emergency access reviews
        var reviewedAccesses = await _context.BreakGlassAccesses
            .Where(b => b.ActivatedAt >= startDate && b.ActivatedAt <= endDate &&
                        b.ReviewedAt != null)
            .CountAsync();

        metrics["reviewed_accesses"] = reviewedAccesses;
        metrics["review_rate"] = breakGlassAccesses > 0 ? (double)reviewedAccesses / breakGlassAccesses * 100 : 100;

        // Check emergency access duration (should be time-limited)
        var timedAccesses = await _context.BreakGlassAccesses
            .Where(b => b.ActivatedAt >= startDate && b.ActivatedAt <= endDate &&
                        b.ExpiresAt != null)
            .CountAsync();

        metrics["timed_accesses"] = timedAccesses;
        metrics["time_limit_compliance"] = breakGlassAccesses > 0 ? (double)timedAccesses / breakGlassAccesses * 100 : 100;

        // Calculate compliance score
        var policyScore = breakGlassPolicies > 0 ? 100.0 : 0.0;
        var auditScore = breakGlassAccesses > 0 ? (double)emergencyAccessWithAudit / breakGlassAccesses * 100 : 100;
        var reviewScore = breakGlassAccesses > 0 ? (double)reviewedAccesses / breakGlassAccesses * 100 : 100;
        var timeLimitScore = breakGlassAccesses > 0 ? (double)timedAccesses / breakGlassAccesses * 100 : 100;

        var overallScore = (policyScore + auditScore + reviewScore + timeLimitScore) / 4;

        var status = overallScore >= 90 ? "implemented" : overallScore >= 70 ? "partial" : "not_implemented";

        var evidence = $"Break-glass policies: {breakGlassPolicies}, Emergency activations: {breakGlassAccesses}, Audit compliance: {auditScore:F2}%, Review rate: {reviewScore:F2}%";
        var implementation = status == "implemented"
            ? "Emergency access procedures are implemented with break-glass capabilities, audit trails, time limits, and post-access reviews."
            : "Emergency access procedures need enhancement.";

        var gaps = status != "implemented"
            ? breakGlassPolicies == 0
                ? "Implement break-glass emergency access policies and procedures."
                : $"Ensure all emergency access has justification ({breakGlassAccesses - emergencyAccessWithAudit} missing), reviews ({breakGlassAccesses - reviewedAccesses} pending), and time limits."
            : null;

        return new ControlAssessmentResult
        {
            ControlId = "HIPAA-164.312(a)(2)(i)",
            ControlName = "Emergency Access Procedure",
            Status = status,
            Score = overallScore,
            Implementation = implementation,
            Evidence = evidence,
            Gaps = gaps,
            Metrics = metrics
        };
    }

    public async Task<ControlAssessmentResult> Assess164_312_d_Async(DateTime startDate, DateTime endDate)
    {
        _logger.LogInformation("Assessing HIPAA §164.312(d) - Person or entity authentication");

        var metrics = new Dictionary<string, object>();

        // Check authentication methods
        var totalUsers = await _context.Users.CountAsync();
        var usersWithMfa = await _context.MfaDevices
            .Where(d => d.IsActive)
            .Select(d => d.UserId)
            .Distinct()
            .CountAsync();

        metrics["total_users"] = totalUsers;
        metrics["users_with_mfa"] = usersWithMfa;
        metrics["mfa_coverage"] = totalUsers > 0 ? (double)usersWithMfa / totalUsers * 100 : 0;

        // Check WebAuthn/FIDO2 usage (stronger authentication)
        var usersWithWebAuthn = await _context.WebAuthnCredentials
            .Where(c => c.IsActive)
            .Select(c => c.UserId)
            .Distinct()
            .CountAsync();

        metrics["users_with_webauthn"] = usersWithWebAuthn;

        // Check authentication success/failure rates
        var successfulLogins = await _context.AuditLogs
            .Where(a => a.Action == "LoginSuccess" && a.CreatedAt >= startDate && a.CreatedAt <= endDate)
            .CountAsync();

        var failedLogins = await _context.AuditLogs
            .Where(a => a.Action == "LoginFailed" && a.CreatedAt >= startDate && a.CreatedAt <= endDate)
            .CountAsync();

        metrics["successful_logins"] = successfulLogins;
        metrics["failed_logins"] = failedLogins;
        metrics["login_success_rate"] = (successfulLogins + failedLogins) > 0
            ? (double)successfulLogins / (successfulLogins + failedLogins) * 100
            : 100;

        // Check for brute force protection (excessive failed attempts)
        var usersWithExcessiveFailures = await _context.AuditLogs
            .Where(a => a.Action == "LoginFailed" && a.CreatedAt >= startDate && a.CreatedAt <= endDate)
            .GroupBy(a => a.UserId)
            .Where(g => g.Count() > 10)
            .CountAsync();

        metrics["users_with_excessive_failures"] = usersWithExcessiveFailures;

        // Check certificate-based authentication
        var usersWithCertificates = await _context.UserRoles
            .Where(ur => ur.Role.Name.Contains("Certificate"))
            .Select(ur => ur.UserId)
            .Distinct()
            .CountAsync();

        metrics["users_with_certificates"] = usersWithCertificates;

        // Calculate compliance score
        var mfaScore = totalUsers > 0 ? (double)usersWithMfa / totalUsers * 100 : 0;
        var webAuthnScore = usersWithWebAuthn > 0 ? 100.0 : 80.0; // Bonus for WebAuthn
        var loginSuccessScore = (successfulLogins + failedLogins) > 0
            ? (double)successfulLogins / (successfulLogins + failedLogins) * 100
            : 100;
        var bruteForceScore = usersWithExcessiveFailures == 0 ? 100.0 : Math.Max(0, 100 - (usersWithExcessiveFailures * 10));

        var overallScore = (mfaScore + webAuthnScore + loginSuccessScore + bruteForceScore) / 4;

        var status = mfaScore >= 80 && bruteForceScore == 100 ? "implemented" : mfaScore >= 50 ? "partial" : "not_implemented";

        var evidence = $"MFA coverage: {mfaScore:F2}%, WebAuthn users: {usersWithWebAuthn}, Login success: {loginSuccessScore:F2}%, Brute force incidents: {usersWithExcessiveFailures}";
        var implementation = status == "implemented"
            ? "Person or entity authentication is implemented with multi-factor authentication, WebAuthn/FIDO2 support, and brute force protection."
            : "Authentication mechanisms need enhancement.";

        var gaps = status != "implemented"
            ? $"Increase MFA adoption to {totalUsers - usersWithMfa} remaining users. Address {usersWithExcessiveFailures} accounts with excessive login failures. Consider mandatory WebAuthn for privileged access."
            : null;

        return new ControlAssessmentResult
        {
            ControlId = "HIPAA-164.312(d)",
            ControlName = "Person or Entity Authentication",
            Status = status,
            Score = overallScore,
            Implementation = implementation,
            Evidence = evidence,
            Gaps = gaps,
            Metrics = metrics
        };
    }

    public async Task<ComplianceReportDto> GenerateReportAsync(DateTime startDate, DateTime endDate, Guid generatedBy)
    {
        _logger.LogInformation("Generating HIPAA compliance report from {StartDate} to {EndDate}", startDate, endDate);

        var assessments = new List<ControlAssessmentResult>
        {
            await Assess164_308_a3_Async(startDate, endDate),
            await Assess164_308_a4_Async(startDate, endDate),
            await Assess164_312_a1_Async(startDate, endDate),
            await Assess164_312_a2i_Async(startDate, endDate),
            await Assess164_312_d_Async(startDate, endDate)
        };

        var implementedCount = assessments.Count(a => a.Status == "implemented");
        var partialCount = assessments.Count(a => a.Status == "partial");
        var notImplementedCount = assessments.Count(a => a.Status == "not_implemented");
        var complianceScore = assessments.Average(a => a.Score);

        var report = new ComplianceReportDto
        {
            Id = Guid.NewGuid(),
            Framework = "HIPAA",
            ReportType = "Security Rule Assessment",
            GeneratedAt = DateTime.UtcNow,
            PeriodStart = startDate,
            PeriodEnd = endDate,
            Status = "completed",
            Format = "JSON",
            TotalControls = assessments.Count,
            ImplementedControls = implementedCount,
            PartialControls = partialCount,
            NotImplementedControls = notImplementedCount,
            ComplianceScore = Math.Round(complianceScore, 2),
            Summary = $"HIPAA Security Rule assessment shows {(complianceScore >= 90 ? "excellent" : complianceScore >= 75 ? "good" : "needs improvement")} compliance with a score of {complianceScore:F2}%. " +
                      $"{implementedCount} controls fully implemented, {partialCount} partially implemented, and {notImplementedCount} not implemented. " +
                      $"Protected Health Information (PHI) is secured through authentication, access controls, and audit mechanisms.",
            Recommendations = GenerateRecommendations(assessments)
        };

        return report;
    }

    public async Task<List<PhiAccessRecord>> GetPhiAccessLogAsync(DateTime startDate, DateTime endDate)
    {
        _logger.LogInformation("Retrieving PHI access log from {StartDate} to {EndDate}", startDate, endDate);

        // PHI access is tracked through audit logs with specific resource types
        var phiResourceTypes = new[] { "Patient", "MedicalRecord", "HealthData", "PHI" };

        var accessLogs = await _context.AuditLogs
            .Where(a => phiResourceTypes.Contains(a.ResourceType) &&
                        a.CreatedAt >= startDate && a.CreatedAt <= endDate)
            .Include(a => a.User)
            .OrderByDescending(a => a.CreatedAt)
            .Take(1000) // Limit for performance
            .ToListAsync();

        var phiAccess = accessLogs.Select(log => new PhiAccessRecord
        {
            Id = log.Id,
            UserId = log.UserId ?? Guid.Empty,
            UserName = log.User?.UserName ?? "Unknown",
            Action = log.Action,
            ResourceType = log.ResourceType,
            ResourceId = log.ResourceId,
            AccessedAt = log.CreatedAt,
            IpAddress = log.IpAddress,
            Purpose = DeterminePurpose(log.Action) // Treatment, Payment, or Operations
        }).ToList();

        _logger.LogInformation("Retrieved {Count} PHI access records", phiAccess.Count);

        return phiAccess;
    }

    public async Task<BaaComplianceStatus> VerifyBaaComplianceAsync()
    {
        _logger.LogInformation("Verifying Business Associate Agreement compliance");

        var totalPartners = await _context.BusinessAssociateAgreements.CountAsync();

        var activeAgreements = await _context.BusinessAssociateAgreements
            .Where(b => b.Status == "Active" &&
                        b.ExpirationDate > DateTime.UtcNow)
            .CountAsync();

        var expiringAgreements = await _context.BusinessAssociateAgreements
            .Where(b => b.Status == "Active" &&
                        b.ExpirationDate <= DateTime.UtcNow.AddDays(b.NotifyDaysBeforeExpiration) &&
                        b.ExpirationDate > DateTime.UtcNow)
            .CountAsync();

        var expiredAgreements = await _context.BusinessAssociateAgreements
            .Where(b => b.Status == "Active" &&
                        b.ExpirationDate <= DateTime.UtcNow)
            .CountAsync();

        var issues = new List<string>();

        if (expiredAgreements > 0)
        {
            issues.Add($"{expiredAgreements} BAA(s) have expired and require immediate renewal");
        }

        if (expiringAgreements > 0)
        {
            issues.Add($"{expiringAgreements} BAA(s) are expiring soon and require renewal");
        }

        var compliancePercentage = totalPartners > 0
            ? (double)activeAgreements / totalPartners * 100
            : 100;

        _logger.LogInformation(
            "BAA Compliance: {Active}/{Total} active, {Expiring} expiring soon, {Expired} expired",
            activeAgreements, totalPartners, expiringAgreements, expiredAgreements);

        return new BaaComplianceStatus
        {
            IsCompliant = expiredAgreements == 0 && compliancePercentage >= 100,
            TotalAgreements = totalPartners,
            ActiveAgreements = activeAgreements,
            ExpiringSoon = expiringAgreements,
            ExpiredAgreements = expiredAgreements,
            CompliancePercentage = compliancePercentage,
            Issues = issues
        };
    }

    private string GenerateRecommendations(List<ControlAssessmentResult> assessments)
    {
        var recommendations = new List<string>();

        foreach (var assessment in assessments.Where(a => a.Status != "implemented"))
        {
            if (!string.IsNullOrEmpty(assessment.Gaps))
            {
                recommendations.Add($"{assessment.ControlId}: {assessment.Gaps}");
            }
        }

        if (recommendations.Count == 0)
        {
            return "All HIPAA Security Rule requirements are implemented. Continue monitoring PHI access and maintaining audit trails for 7 years.";
        }

        return "Recommendations:\n" + string.Join("\n", recommendations.Select((r, i) => $"{i + 1}. {r}"));
    }

    private string DeterminePurpose(string action)
    {
        // Determine purpose based on action
        if (action.Contains("Treatment") || action.Contains("Medical"))
            return "Treatment";
        if (action.Contains("Payment") || action.Contains("Billing"))
            return "Payment";
        return "Operations";
    }
}
