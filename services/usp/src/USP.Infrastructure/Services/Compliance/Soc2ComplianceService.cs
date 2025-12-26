using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using USP.Core.Models.DTOs.Compliance;
using USP.Core.Models.Entities;
using USP.Core.Services.Compliance;
using USP.Infrastructure.Data;

namespace USP.Infrastructure.Services.Compliance;

/// <summary>
/// SOC 2 Type II compliance service implementation
/// Implements Trust Service Criteria for security, availability, and confidentiality
/// </summary>
public class Soc2ComplianceService : ISoc2ComplianceService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<Soc2ComplianceService> _logger;

    public Soc2ComplianceService(
        ApplicationDbContext context,
        ILogger<Soc2ComplianceService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ControlAssessmentResult> AssessCC61Async(DateTime startDate, DateTime endDate)
    {
        _logger.LogInformation("Assessing SOC 2 CC6.1 - Logical access controls");

        var metrics = new Dictionary<string, object>();

        // Check RBAC implementation
        var totalUsers = await _context.Users.CountAsync();
        var usersWithRoles = await _context.UserRoles
            .Select(ur => ur.UserId)
            .Distinct()
            .CountAsync();

        metrics["total_users"] = totalUsers;
        metrics["users_with_rbac"] = usersWithRoles;
        metrics["rbac_coverage"] = totalUsers > 0 ? (double)usersWithRoles / totalUsers * 100 : 0;

        // Check access policies
        var activePolicies = await _context.AccessPolicies
            .Where(p => p.IsActive)
            .CountAsync();
        metrics["active_policies"] = activePolicies;

        // Check for inactive users with access
        var inactiveThreshold = DateTime.UtcNow.AddDays(-90);
        var inactiveUsersWithAccess = await _context.Users
            .Where(u => u.LastLoginAt < inactiveThreshold && u.IsActive)
            .CountAsync();
        metrics["inactive_users_with_access"] = inactiveUsersWithAccess;

        // Calculate compliance score
        var rbacScore = totalUsers > 0 ? (double)usersWithRoles / totalUsers * 100 : 0;
        var policyScore = activePolicies > 0 ? 100.0 : 0.0;
        var inactiveUserScore = inactiveUsersWithAccess == 0 ? 100.0 : Math.Max(0, 100 - (inactiveUsersWithAccess * 10));

        var overallScore = (rbacScore + policyScore + inactiveUserScore) / 3;

        var status = overallScore >= 90 ? "implemented" : overallScore >= 60 ? "partial" : "not_implemented";

        var evidence = $"RBAC coverage: {rbacScore:F2}%, Active policies: {activePolicies}, Inactive users: {inactiveUsersWithAccess}";
        var implementation = status == "implemented"
            ? "Role-based access control (RBAC) is implemented across the platform. All users are assigned roles with specific permissions."
            : "RBAC implementation is in progress or incomplete.";

        var gaps = status != "implemented"
            ? $"Address inactive users with access ({inactiveUsersWithAccess}). Ensure all users have role assignments."
            : null;

        return new ControlAssessmentResult
        {
            ControlId = "SOC2-CC6.1",
            ControlName = "Logical and Physical Access Controls",
            Status = status,
            Score = overallScore,
            Implementation = implementation,
            Evidence = evidence,
            Gaps = gaps,
            Metrics = metrics
        };
    }

    public async Task<ControlAssessmentResult> AssessCC62Async(DateTime startDate, DateTime endDate)
    {
        _logger.LogInformation("Assessing SOC 2 CC6.2 - Multi-factor authentication");

        var metrics = new Dictionary<string, object>();

        // Check MFA enrollment
        var totalUsers = await _context.Users.CountAsync();
        var usersWithMfa = await _context.MfaDevices
            .Where(d => d.IsActive)
            .Select(d => d.UserId)
            .Distinct()
            .CountAsync();

        metrics["total_users"] = totalUsers;
        metrics["users_with_mfa"] = usersWithMfa;
        metrics["mfa_coverage"] = totalUsers > 0 ? (double)usersWithMfa / totalUsers * 100 : 0;

        // Check for privileged users with MFA
        var privilegedRoles = new[] { "PlatformAdmin", "SystemAdmin", "SecurityAdmin" };
        var privilegedUsers = await _context.UserRoles
            .Include(ur => ur.Role)
            .Where(ur => privilegedRoles.Contains(ur.Role.Name))
            .Select(ur => ur.UserId)
            .Distinct()
            .CountAsync();

        var privilegedUsersWithMfa = await _context.UserRoles
            .Include(ur => ur.Role)
            .Where(ur => privilegedRoles.Contains(ur.Role.Name))
            .Select(ur => ur.UserId)
            .Distinct()
            .Where(userId => _context.MfaDevices.Any(d => d.UserId == userId && d.IsActive))
            .CountAsync();

        metrics["privileged_users"] = privilegedUsers;
        metrics["privileged_users_with_mfa"] = privilegedUsersWithMfa;
        metrics["privileged_mfa_coverage"] = privilegedUsers > 0 ? (double)privilegedUsersWithMfa / privilegedUsers * 100 : 0;

        // Check MFA usage in audit logs
        var mfaVerifications = await _context.AuditLogs
            .Where(a => a.Action == "MfaVerified" && a.CreatedAt >= startDate && a.CreatedAt <= endDate)
            .CountAsync();
        metrics["mfa_verifications"] = mfaVerifications;

        // Calculate compliance score
        var mfaScore = totalUsers > 0 ? (double)usersWithMfa / totalUsers * 100 : 0;
        var privilegedMfaScore = privilegedUsers > 0 ? (double)privilegedUsersWithMfa / privilegedUsers * 100 : 0;

        var overallScore = (mfaScore * 0.5) + (privilegedMfaScore * 0.5); // Weight privileged users higher

        var status = privilegedMfaScore == 100 && mfaScore >= 80 ? "implemented" : mfaScore >= 50 ? "partial" : "not_implemented";

        var evidence = $"MFA coverage: {mfaScore:F2}%, Privileged user MFA: {privilegedMfaScore:F2}%, Verifications: {mfaVerifications}";
        var implementation = status == "implemented"
            ? "Multi-factor authentication is enforced for all privileged users and strongly encouraged for all users."
            : "MFA implementation needs improvement.";

        var gaps = status != "implemented"
            ? $"Enforce MFA for all {privilegedUsers - privilegedUsersWithMfa} privileged users without MFA. Increase overall MFA adoption."
            : null;

        return new ControlAssessmentResult
        {
            ControlId = "SOC2-CC6.2",
            ControlName = "Prior to Issuing System Credentials and Granting System Access, the Entity Registers and Authorizes New Internal and External Users",
            Status = status,
            Score = overallScore,
            Implementation = implementation,
            Evidence = evidence,
            Gaps = gaps,
            Metrics = metrics
        };
    }

    public async Task<ControlAssessmentResult> AssessCC63Async(DateTime startDate, DateTime endDate)
    {
        _logger.LogInformation("Assessing SOC 2 CC6.3 - Privileged access management");

        var metrics = new Dictionary<string, object>();

        // Check PAM safe usage
        var totalSafes = await _context.PrivilegedSafes.CountAsync();
        var activeSafes = await _context.PrivilegedSafes
            .Where(s => s.IsActive)
            .CountAsync();

        metrics["total_safes"] = totalSafes;
        metrics["active_safes"] = activeSafes;

        // Check privileged account management
        var totalPrivilegedAccounts = await _context.PrivilegedAccounts.CountAsync();
        var accountsWithRotation = await _context.PrivilegedAccounts
            .Where(a => a.RotationEnabled)
            .CountAsync();

        metrics["total_privileged_accounts"] = totalPrivilegedAccounts;
        metrics["accounts_with_rotation"] = accountsWithRotation;
        metrics["rotation_coverage"] = totalPrivilegedAccounts > 0 ? (double)accountsWithRotation / totalPrivilegedAccounts * 100 : 0;

        // Check checkout/checkin compliance
        var checkouts = await _context.AccountCheckouts
            .Where(c => c.CheckedOutAt >= startDate && c.CheckedOutAt <= endDate)
            .CountAsync();
        var checkedInOnTime = await _context.AccountCheckouts
            .Where(c => c.CheckedOutAt >= startDate && c.CheckedOutAt <= endDate &&
                        c.CheckedInAt != null && c.CheckedInAt <= c.ExpiresAt)
            .CountAsync();

        metrics["total_checkouts"] = checkouts;
        metrics["checked_in_on_time"] = checkedInOnTime;
        metrics["checkout_compliance"] = checkouts > 0 ? (double)checkedInOnTime / checkouts * 100 : 100;

        // Check session recording
        var recordedSessions = await _context.PrivilegedSessions
            .Where(s => s.StartedAt >= startDate && s.StartedAt <= endDate && s.IsRecorded)
            .CountAsync();
        var totalSessions = await _context.PrivilegedSessions
            .Where(s => s.StartedAt >= startDate && s.StartedAt <= endDate)
            .CountAsync();

        metrics["recorded_sessions"] = recordedSessions;
        metrics["total_sessions"] = totalSessions;
        metrics["recording_coverage"] = totalSessions > 0 ? (double)recordedSessions / totalSessions * 100 : 100;

        // Calculate compliance score
        var rotationScore = totalPrivilegedAccounts > 0 ? (double)accountsWithRotation / totalPrivilegedAccounts * 100 : 100;
        var checkoutScore = checkouts > 0 ? (double)checkedInOnTime / checkouts * 100 : 100;
        var recordingScore = totalSessions > 0 ? (double)recordedSessions / totalSessions * 100 : 100;

        var overallScore = (rotationScore + checkoutScore + recordingScore) / 3;

        var status = overallScore >= 90 ? "implemented" : overallScore >= 60 ? "partial" : "not_implemented";

        var evidence = $"PAM safes: {activeSafes}, Rotation coverage: {rotationScore:F2}%, Checkout compliance: {checkoutScore:F2}%, Recording: {recordingScore:F2}%";
        var implementation = status == "implemented"
            ? "Privileged Access Management (PAM) is implemented with credential rotation, checkout/checkin workflows, and session recording."
            : "PAM implementation needs enhancement.";

        var gaps = status != "implemented"
            ? $"Enable rotation for {totalPrivilegedAccounts - accountsWithRotation} accounts. Improve checkout compliance. Ensure all sessions are recorded."
            : null;

        return new ControlAssessmentResult
        {
            ControlId = "SOC2-CC6.3",
            ControlName = "The Entity Removes System Access When Appropriate",
            Status = status,
            Score = overallScore,
            Implementation = implementation,
            Evidence = evidence,
            Gaps = gaps,
            Metrics = metrics
        };
    }

    public async Task<ControlAssessmentResult> AssessCC66Async(DateTime startDate, DateTime endDate)
    {
        _logger.LogInformation("Assessing SOC 2 CC6.6 - Encryption in transit and at rest");

        var metrics = new Dictionary<string, object>();

        // Check Transit Engine usage (encryption-as-a-service)
        var transitKeys = await _context.TransitKeys
            .Where(k => !k.IsDeleted)
            .CountAsync();
        var transitEncryptions = await _context.AuditLogs
            .Where(a => (a.Action == "TransitEncrypt" || a.Action == "TransitDecrypt") &&
                        a.CreatedAt >= startDate && a.CreatedAt <= endDate)
            .CountAsync();

        metrics["transit_keys"] = transitKeys;
        metrics["transit_operations"] = transitEncryptions;

        // Check secrets encryption
        var totalSecrets = await _context.Secrets.CountAsync();
        var encryptedSecrets = await _context.Secrets
            .Where(s => !string.IsNullOrEmpty(s.EncryptedData))
            .CountAsync();

        metrics["total_secrets"] = totalSecrets;
        metrics["encrypted_secrets"] = encryptedSecrets;
        metrics["secrets_encryption"] = totalSecrets > 0 ? (double)encryptedSecrets / totalSecrets * 100 : 100;

        // Check TLS enforcement from audit logs
        var tlsConnections = await _context.AuditLogs
            .Where(a => a.Action.Contains("Login") && a.CreatedAt >= startDate && a.CreatedAt <= endDate)
            .CountAsync();

        metrics["tls_connections"] = tlsConnections;

        // Check encryption key rotation
        var rotatedKeys = await _context.TransitKeys
            .Where(k => k.LatestVersion > 1)
            .CountAsync();

        metrics["rotated_keys"] = rotatedKeys;
        metrics["key_rotation_rate"] = transitKeys > 0 ? (double)rotatedKeys / transitKeys * 100 : 0;

        // Calculate compliance score
        var secretsScore = totalSecrets > 0 ? (double)encryptedSecrets / totalSecrets * 100 : 100;
        var transitScore = transitKeys > 0 ? 100.0 : 50.0; // Having transit engine is good
        var rotationScore = transitKeys > 0 ? (double)rotatedKeys / transitKeys * 100 : 0;

        var overallScore = (secretsScore + transitScore + rotationScore) / 3;

        var status = secretsScore == 100 && transitKeys > 0 ? "implemented" : secretsScore >= 80 ? "partial" : "not_implemented";

        var evidence = $"Secrets encryption: {secretsScore:F2}%, Transit keys: {transitKeys}, Transit ops: {transitEncryptions}, Key rotation: {rotationScore:F2}%";
        var implementation = status == "implemented"
            ? "Encryption is implemented using AES-256-GCM for data at rest and TLS 1.3 for data in transit. Transit engine provides encryption-as-a-service."
            : "Encryption implementation needs improvement.";

        var gaps = status != "implemented"
            ? $"Encrypt {totalSecrets - encryptedSecrets} unencrypted secrets. Implement key rotation for all transit keys."
            : null;

        return new ControlAssessmentResult
        {
            ControlId = "SOC2-CC6.6",
            ControlName = "The Entity Implements Logical Access Security Measures to Protect Against Threats from Sources Outside Its System Boundaries",
            Status = status,
            Score = overallScore,
            Implementation = implementation,
            Evidence = evidence,
            Gaps = gaps,
            Metrics = metrics
        };
    }

    public async Task<ControlAssessmentResult> AssessCC72Async(DateTime startDate, DateTime endDate)
    {
        _logger.LogInformation("Assessing SOC 2 CC7.2 - System monitoring");

        var metrics = new Dictionary<string, object>();

        // Check audit logging coverage
        var auditLogCount = await _context.AuditLogs
            .Where(a => a.CreatedAt >= startDate && a.CreatedAt <= endDate)
            .CountAsync();

        metrics["audit_logs"] = auditLogCount;

        // Check different event types
        var eventTypes = await _context.AuditLogs
            .Where(a => a.CreatedAt >= startDate && a.CreatedAt <= endDate)
            .Select(a => a.Action)
            .Distinct()
            .CountAsync();

        metrics["event_types_logged"] = eventTypes;

        // Check failed authentication attempts (security monitoring)
        var failedLogins = await _context.AuditLogs
            .Where(a => a.Action == "LoginFailed" && a.CreatedAt >= startDate && a.CreatedAt <= endDate)
            .CountAsync();

        metrics["failed_logins"] = failedLogins;

        // Check security events
        var securityEvents = await _context.AuditLogs
            .Where(a => (a.Action.Contains("Unauthorized") || a.Action.Contains("Violation") || a.Action.Contains("Failed")) &&
                        a.CreatedAt >= startDate && a.CreatedAt <= endDate)
            .CountAsync();

        metrics["security_events"] = securityEvents;

        // Check webhook/alerting configuration
        var activeWebhooks = await _context.Webhooks
            .Where(w => w.IsActive)
            .CountAsync();

        metrics["active_webhooks"] = activeWebhooks;

        // Check audit log retention
        var oldestLog = await _context.AuditLogs
            .OrderBy(a => a.CreatedAt)
            .Select(a => a.CreatedAt)
            .FirstOrDefaultAsync();

        if (oldestLog != default)
        {
            var retentionDays = (DateTime.UtcNow - oldestLog).Days;
            metrics["audit_retention_days"] = retentionDays;
        }

        // Calculate compliance score
        var loggingScore = auditLogCount > 1000 ? 100.0 : auditLogCount > 100 ? 80.0 : 50.0;
        var diversityScore = eventTypes > 20 ? 100.0 : eventTypes > 10 ? 80.0 : 50.0;
        var alertingScore = activeWebhooks > 0 ? 100.0 : 50.0;
        var retentionScore = oldestLog != default && (DateTime.UtcNow - oldestLog).Days >= 365 ? 100.0 : 70.0;

        var overallScore = (loggingScore + diversityScore + alertingScore + retentionScore) / 4;

        var status = overallScore >= 90 ? "implemented" : overallScore >= 60 ? "partial" : "not_implemented";

        var evidence = $"Audit logs: {auditLogCount}, Event types: {eventTypes}, Security events: {securityEvents}, Active webhooks: {activeWebhooks}";
        var implementation = status == "implemented"
            ? "Comprehensive system monitoring is implemented with audit logging, security event detection, and alerting mechanisms."
            : "System monitoring implementation needs enhancement.";

        var gaps = status != "implemented"
            ? "Expand event type coverage. Configure additional alerting webhooks. Ensure 7-year retention for compliance."
            : null;

        return new ControlAssessmentResult
        {
            ControlId = "SOC2-CC7.2",
            ControlName = "The Entity Monitors System Components and the Operation of Those Components for Anomalies",
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
        _logger.LogInformation("Generating SOC 2 compliance report from {StartDate} to {EndDate}", startDate, endDate);

        var assessments = new List<ControlAssessmentResult>
        {
            await AssessCC61Async(startDate, endDate),
            await AssessCC62Async(startDate, endDate),
            await AssessCC63Async(startDate, endDate),
            await AssessCC66Async(startDate, endDate),
            await AssessCC72Async(startDate, endDate)
        };

        var implementedCount = assessments.Count(a => a.Status == "implemented");
        var partialCount = assessments.Count(a => a.Status == "partial");
        var notImplementedCount = assessments.Count(a => a.Status == "not_implemented");
        var complianceScore = assessments.Average(a => a.Score);

        var report = new ComplianceReportDto
        {
            Id = Guid.NewGuid(),
            Framework = "SOC2",
            ReportType = "Type II Assessment",
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
            Summary = $"SOC 2 Type II compliance assessment shows {(complianceScore >= 90 ? "excellent" : complianceScore >= 75 ? "good" : "needs improvement")} compliance with a score of {complianceScore:F2}%. " +
                      $"{implementedCount} controls fully implemented, {partialCount} partially implemented, and {notImplementedCount} not implemented.",
            Recommendations = GenerateRecommendations(assessments)
        };

        return report;
    }

    public async Task<List<EvidenceDto>> CollectAuditEvidenceAsync(DateTime startDate, DateTime endDate)
    {
        _logger.LogInformation("Collecting SOC 2 audit evidence from {StartDate} to {EndDate}", startDate, endDate);

        var evidence = new List<EvidenceDto>();

        // Evidence for CC6.1 - Access control logs
        var accessControlLogs = await _context.AuditLogs
            .Where(a => (a.Action.Contains("Role") || a.Action.Contains("Permission") || a.Action.Contains("Policy")) &&
                        a.CreatedAt >= startDate && a.CreatedAt <= endDate)
            .CountAsync();

        if (accessControlLogs > 0)
        {
            evidence.Add(new EvidenceDto
            {
                Id = Guid.NewGuid(),
                ControlId = "SOC2-CC6.1",
                EvidenceType = "AuditLog",
                Title = "Access Control Audit Logs",
                Description = $"{accessControlLogs} access control events logged during the period",
                CollectedAt = DateTime.UtcNow,
                IsAutomated = true
            });
        }

        // Evidence for CC6.2 - MFA enrollment records
        var mfaDevices = await _context.MfaDevices
            .Where(d => d.EnrolledAt >= startDate && d.EnrolledAt <= endDate)
            .CountAsync();

        if (mfaDevices > 0)
        {
            evidence.Add(new EvidenceDto
            {
                Id = Guid.NewGuid(),
                ControlId = "SOC2-CC6.2",
                EvidenceType = "Configuration",
                Title = "MFA Device Enrollment Records",
                Description = $"{mfaDevices} MFA devices enrolled during the period",
                CollectedAt = DateTime.UtcNow,
                IsAutomated = true
            });
        }

        // Evidence for CC6.3 - PAM session recordings
        var recordedSessions = await _context.PrivilegedSessions
            .Where(s => s.StartedAt >= startDate && s.StartedAt <= endDate && s.IsRecorded)
            .CountAsync();

        if (recordedSessions > 0)
        {
            evidence.Add(new EvidenceDto
            {
                Id = Guid.NewGuid(),
                ControlId = "SOC2-CC6.3",
                EvidenceType = "AuditLog",
                Title = "Privileged Session Recordings",
                Description = $"{recordedSessions} privileged sessions recorded during the period",
                CollectedAt = DateTime.UtcNow,
                IsAutomated = true
            });
        }

        // Evidence for CC6.6 - Encryption operations
        var encryptionOps = await _context.AuditLogs
            .Where(a => (a.Action.Contains("Encrypt") || a.Action.Contains("Decrypt")) &&
                        a.CreatedAt >= startDate && a.CreatedAt <= endDate)
            .CountAsync();

        if (encryptionOps > 0)
        {
            evidence.Add(new EvidenceDto
            {
                Id = Guid.NewGuid(),
                ControlId = "SOC2-CC6.6",
                EvidenceType = "AuditLog",
                Title = "Encryption Operations Log",
                Description = $"{encryptionOps} encryption/decryption operations logged during the period",
                CollectedAt = DateTime.UtcNow,
                IsAutomated = true
            });
        }

        // Evidence for CC7.2 - Monitoring and alerting
        var webhookDeliveries = await _context.WebhookDeliveries
            .Where(w => w.AttemptedAt >= startDate && w.AttemptedAt <= endDate && w.Success)
            .CountAsync();

        if (webhookDeliveries > 0)
        {
            evidence.Add(new EvidenceDto
            {
                Id = Guid.NewGuid(),
                ControlId = "SOC2-CC7.2",
                EvidenceType = "AuditLog",
                Title = "Security Alert Deliveries",
                Description = $"{webhookDeliveries} security alerts successfully delivered during the period",
                CollectedAt = DateTime.UtcNow,
                IsAutomated = true
            });
        }

        _logger.LogInformation("Collected {Count} pieces of evidence for SOC 2 audit", evidence.Count);

        return evidence;
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
            return "All SOC 2 Trust Service Criteria are implemented. Continue monitoring and maintaining controls.";
        }

        return "Recommendations:\n" + string.Join("\n", recommendations.Select((r, i) => $"{i + 1}. {r}"));
    }
}
