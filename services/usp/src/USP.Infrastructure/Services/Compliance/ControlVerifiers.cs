using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using USP.Core.Models.DTOs.Compliance;
using USP.Core.Models.Entities;
using USP.Core.Services.Compliance;
using USP.Infrastructure.Data;

namespace USP.Infrastructure.Services.Compliance;

/// <summary>
/// Verifies access control compliance
/// Checks RBAC, ABAC, permissions, and access policies
/// </summary>
public class AccessControlVerifier : IControlVerifier
{
    private readonly ApplicationDbContext _context;

    public AccessControlVerifier(ApplicationDbContext context)
    {
        _context = context;
    }

    public bool CanVerify(string controlType)
    {
        return controlType.Contains("access", StringComparison.OrdinalIgnoreCase) ||
               controlType.Contains("authorization", StringComparison.OrdinalIgnoreCase) ||
               controlType.Contains("permission", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ControlVerificationResultDto> VerifyAsync(Guid controlId)
    {
        var startTime = DateTime.UtcNow;
        var evidence = await CollectEvidenceAsync(controlId);
        var control = await _context.ComplianceControls.FindAsync(controlId);

        var issues = new List<string>();
        var recommendations = new List<string>();
        int score = 100;

        // Check if RBAC is configured
        var roleCount = await _context.Roles.CountAsync();
        if (roleCount == 0)
        {
            issues.Add("No roles configured - RBAC not implemented");
            recommendations.Add("Configure role-based access control with appropriate roles");
            score -= 30;
        }

        // Check if permissions are assigned
        var permissionCount = await _context.Permissions.CountAsync();
        if (permissionCount == 0)
        {
            issues.Add("No permissions configured");
            recommendations.Add("Define and assign permissions to roles");
            score -= 20;
        }

        // Check if access policies exist
        var policyCount = await _context.AccessPolicies.CountAsync();
        if (policyCount == 0)
        {
            issues.Add("No access policies configured");
            recommendations.Add("Implement attribute-based access control (ABAC) policies");
            score -= 20;
        }

        // Check for users without roles
        var usersWithoutRoles = await _context.Users
            .Where(u => !u.UserRoles.Any())
            .CountAsync();
        if (usersWithoutRoles > 0)
        {
            issues.Add($"{usersWithoutRoles} users without assigned roles");
            recommendations.Add("Assign appropriate roles to all users");
            score -= 10;
        }

        var status = score >= 80 ? "pass" : score >= 60 ? "warning" : "fail";
        var duration = (int)(DateTime.UtcNow - startTime).TotalSeconds;

        return new ControlVerificationResultDto
        {
            VerificationId = Guid.NewGuid(),
            ControlId = controlId,
            ControlName = control?.Name ?? "Access Control",
            ControlDescription = control?.Description ?? "",
            VerifiedAt = DateTime.UtcNow,
            Status = status,
            Score = Math.Max(0, score),
            Evidence = evidence.Items,
            Findings = $"Verified {evidence.TotalItems} access control components",
            Issues = issues,
            Recommendations = recommendations,
            VerificationMethod = "automated",
            DurationSeconds = duration
        };
    }

    public async Task<ControlEvidenceDto> CollectEvidenceAsync(Guid controlId)
    {
        var evidence = new List<EvidenceItemDto>();

        // Collect role evidence
        var roleCount = await _context.Roles.CountAsync();
        evidence.Add(new EvidenceItemDto
        {
            Type = "configuration",
            Description = "Total roles configured",
            Value = roleCount,
            Timestamp = DateTime.UtcNow,
            Source = "Roles table"
        });

        // Collect permission evidence
        var permissionCount = await _context.Permissions.CountAsync();
        evidence.Add(new EvidenceItemDto
        {
            Type = "configuration",
            Description = "Total permissions configured",
            Value = permissionCount,
            Timestamp = DateTime.UtcNow,
            Source = "Permissions table"
        });

        // Collect policy evidence
        var policyCount = await _context.AccessPolicies.CountAsync();
        evidence.Add(new EvidenceItemDto
        {
            Type = "configuration",
            Description = "Total access policies configured",
            Value = policyCount,
            Timestamp = DateTime.UtcNow,
            Source = "AccessPolicies table"
        });

        return new ControlEvidenceDto
        {
            ControlId = controlId,
            CollectedAt = DateTime.UtcNow,
            Items = evidence,
            TotalItems = evidence.Count,
            EvidenceTypeCounts = evidence.GroupBy(e => e.Type).ToDictionary(g => g.Key, g => g.Count())
        };
    }
}

/// <summary>
/// Verifies encryption compliance
/// Checks encryption at rest, in transit, and key management
/// </summary>
public class EncryptionVerifier : IControlVerifier
{
    private readonly ApplicationDbContext _context;

    public EncryptionVerifier(ApplicationDbContext context)
    {
        _context = context;
    }

    public bool CanVerify(string controlType)
    {
        return controlType.Contains("encryption", StringComparison.OrdinalIgnoreCase) ||
               controlType.Contains("crypto", StringComparison.OrdinalIgnoreCase) ||
               controlType.Contains("key", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ControlVerificationResultDto> VerifyAsync(Guid controlId)
    {
        var startTime = DateTime.UtcNow;
        var evidence = await CollectEvidenceAsync(controlId);
        var control = await _context.ComplianceControls.FindAsync(controlId);

        var issues = new List<string>();
        var recommendations = new List<string>();
        int score = 100;

        // Check if transit keys are configured
        var transitKeyCount = await _context.TransitKeys.CountAsync();
        if (transitKeyCount == 0)
        {
            issues.Add("No transit encryption keys configured");
            recommendations.Add("Configure transit encryption keys for data in transit");
            score -= 25;
        }

        // Check if secrets are encrypted
        var secretCount = await _context.Secrets.CountAsync();
        var encryptedSecrets = await _context.Secrets.Where(s => s.EncryptedData != null && s.EncryptedData.Length > 0).CountAsync();
        if (secretCount > 0 && encryptedSecrets < secretCount)
        {
            issues.Add($"{secretCount - encryptedSecrets} secrets not encrypted");
            recommendations.Add("Ensure all secrets are encrypted at rest");
            score -= 30;
        }

        // Check key rotation
        var oldKeys = await _context.TransitKeys
            .Where(k => k.CreatedAt < DateTime.UtcNow.AddDays(-90))
            .CountAsync();
        if (oldKeys > 0)
        {
            issues.Add($"{oldKeys} encryption keys older than 90 days");
            recommendations.Add("Rotate encryption keys regularly (recommended: every 90 days)");
            score -= 15;
        }

        var status = score >= 80 ? "pass" : score >= 60 ? "warning" : "fail";
        var duration = (int)(DateTime.UtcNow - startTime).TotalSeconds;

        return new ControlVerificationResultDto
        {
            VerificationId = Guid.NewGuid(),
            ControlId = controlId,
            ControlName = control?.Name ?? "Encryption Control",
            ControlDescription = control?.Description ?? "",
            VerifiedAt = DateTime.UtcNow,
            Status = status,
            Score = Math.Max(0, score),
            Evidence = evidence.Items,
            Findings = $"Verified encryption configuration for {secretCount} secrets and {transitKeyCount} keys",
            Issues = issues,
            Recommendations = recommendations,
            VerificationMethod = "automated",
            DurationSeconds = duration
        };
    }

    public async Task<ControlEvidenceDto> CollectEvidenceAsync(Guid controlId)
    {
        var evidence = new List<EvidenceItemDto>();

        // Transit keys evidence
        var transitKeyCount = await _context.TransitKeys.CountAsync();
        evidence.Add(new EvidenceItemDto
        {
            Type = "configuration",
            Description = "Transit encryption keys configured",
            Value = transitKeyCount,
            Timestamp = DateTime.UtcNow,
            Source = "TransitKeys table"
        });

        // Encrypted secrets evidence
        var encryptedSecretsCount = await _context.Secrets
            .Where(s => s.EncryptedData != null && s.EncryptedData.Length > 0)
            .CountAsync();
        evidence.Add(new EvidenceItemDto
        {
            Type = "configuration",
            Description = "Encrypted secrets",
            Value = encryptedSecretsCount,
            Timestamp = DateTime.UtcNow,
            Source = "Secrets table"
        });

        return new ControlEvidenceDto
        {
            ControlId = controlId,
            CollectedAt = DateTime.UtcNow,
            Items = evidence,
            TotalItems = evidence.Count,
            EvidenceTypeCounts = evidence.GroupBy(e => e.Type).ToDictionary(g => g.Key, g => g.Count())
        };
    }
}

/// <summary>
/// Verifies audit logging compliance
/// Checks audit trail completeness and retention
/// </summary>
public class AuditVerifier : IControlVerifier
{
    private readonly ApplicationDbContext _context;

    public AuditVerifier(ApplicationDbContext context)
    {
        _context = context;
    }

    public bool CanVerify(string controlType)
    {
        return controlType.Contains("audit", StringComparison.OrdinalIgnoreCase) ||
               controlType.Contains("logging", StringComparison.OrdinalIgnoreCase) ||
               controlType.Contains("trail", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ControlVerificationResultDto> VerifyAsync(Guid controlId)
    {
        var startTime = DateTime.UtcNow;
        var evidence = await CollectEvidenceAsync(controlId);
        var control = await _context.ComplianceControls.FindAsync(controlId);

        var issues = new List<string>();
        var recommendations = new List<string>();
        int score = 100;

        // Check audit log volume (should have logs)
        var auditLogCount = await _context.AuditLogs.CountAsync();
        if (auditLogCount == 0)
        {
            issues.Add("No audit logs found");
            recommendations.Add("Enable comprehensive audit logging for all operations");
            score -= 50;
        }

        // Check recent audit logs (should have logs from last 24 hours)
        var recentLogs = await _context.AuditLogs
            .Where(l => l.CreatedAt > DateTime.UtcNow.AddHours(-24))
            .CountAsync();
        if (recentLogs == 0 && auditLogCount > 0)
        {
            issues.Add("No recent audit logs (last 24 hours)");
            recommendations.Add("Verify audit logging is currently active");
            score -= 30;
        }

        // Check for privileged operations logging
        var privilegedLogs = await _context.AuditLogs
            .Where(l => l.Action.Contains("Admin") || l.Action.Contains("Delete") || l.Action.Contains("Privileged"))
            .CountAsync();
        if (privilegedLogs == 0 && auditLogCount > 100)
        {
            issues.Add("No privileged operations logged");
            recommendations.Add("Ensure all privileged operations are audited");
            score -= 20;
        }

        var status = score >= 80 ? "pass" : score >= 60 ? "warning" : "fail";
        var duration = (int)(DateTime.UtcNow - startTime).TotalSeconds;

        return new ControlVerificationResultDto
        {
            VerificationId = Guid.NewGuid(),
            ControlId = controlId,
            ControlName = control?.Name ?? "Audit Logging",
            ControlDescription = control?.Description ?? "",
            VerifiedAt = DateTime.UtcNow,
            Status = status,
            Score = Math.Max(0, score),
            Evidence = evidence.Items,
            Findings = $"Analyzed {auditLogCount} audit log entries",
            Issues = issues,
            Recommendations = recommendations,
            VerificationMethod = "automated",
            DurationSeconds = duration
        };
    }

    public async Task<ControlEvidenceDto> CollectEvidenceAsync(Guid controlId)
    {
        var evidence = new List<EvidenceItemDto>();

        // Total audit logs
        var totalLogs = await _context.AuditLogs.CountAsync();
        evidence.Add(new EvidenceItemDto
        {
            Type = "audit_log",
            Description = "Total audit log entries",
            Value = totalLogs,
            Timestamp = DateTime.UtcNow,
            Source = "AuditLogs table"
        });

        // Recent logs (last 7 days)
        var recentLogs = await _context.AuditLogs
            .Where(l => l.CreatedAt > DateTime.UtcNow.AddDays(-7))
            .CountAsync();
        evidence.Add(new EvidenceItemDto
        {
            Type = "audit_log",
            Description = "Audit logs (last 7 days)",
            Value = recentLogs,
            Timestamp = DateTime.UtcNow,
            Source = "AuditLogs table"
        });

        return new ControlEvidenceDto
        {
            ControlId = controlId,
            CollectedAt = DateTime.UtcNow,
            Items = evidence,
            TotalItems = evidence.Count,
            EvidenceTypeCounts = evidence.GroupBy(e => e.Type).ToDictionary(g => g.Key, g => g.Count())
        };
    }
}

/// <summary>
/// Verifies monitoring and alerting compliance
/// Checks authentication events, risk assessment, and security monitoring
/// </summary>
public class MonitoringVerifier : IControlVerifier
{
    private readonly ApplicationDbContext _context;

    public MonitoringVerifier(ApplicationDbContext context)
    {
        _context = context;
    }

    public bool CanVerify(string controlType)
    {
        return controlType.Contains("monitoring", StringComparison.OrdinalIgnoreCase) ||
               controlType.Contains("alert", StringComparison.OrdinalIgnoreCase) ||
               controlType.Contains("detection", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ControlVerificationResultDto> VerifyAsync(Guid controlId)
    {
        var startTime = DateTime.UtcNow;
        var evidence = await CollectEvidenceAsync(controlId);
        var control = await _context.ComplianceControls.FindAsync(controlId);

        var issues = new List<string>();
        var recommendations = new List<string>();
        int score = 100;

        // Check authentication event monitoring
        var authEventCount = await _context.AuthenticationEvents.CountAsync();
        if (authEventCount == 0)
        {
            issues.Add("No authentication events monitored");
            recommendations.Add("Enable authentication event monitoring");
            score -= 30;
        }

        // Check risk assessments
        var riskAssessmentCount = await _context.RiskAssessments.CountAsync();
        if (riskAssessmentCount == 0)
        {
            issues.Add("No risk assessments performed");
            recommendations.Add("Enable risk-based authentication and monitoring");
            score -= 25;
        }

        // Check for recent monitoring activity
        var recentAuthEvents = await _context.AuthenticationEvents
            .Where(e => e.EventTime > DateTime.UtcNow.AddHours(-24))
            .CountAsync();
        if (recentAuthEvents == 0 && authEventCount > 0)
        {
            issues.Add("No recent authentication events (last 24 hours)");
            recommendations.Add("Verify monitoring systems are active");
            score -= 20;
        }

        var status = score >= 80 ? "pass" : score >= 60 ? "warning" : "fail";
        var duration = (int)(DateTime.UtcNow - startTime).TotalSeconds;

        return new ControlVerificationResultDto
        {
            VerificationId = Guid.NewGuid(),
            ControlId = controlId,
            ControlName = control?.Name ?? "Security Monitoring",
            ControlDescription = control?.Description ?? "",
            VerifiedAt = DateTime.UtcNow,
            Status = status,
            Score = Math.Max(0, score),
            Evidence = evidence.Items,
            Findings = $"Verified {authEventCount} authentication events and {riskAssessmentCount} risk assessments",
            Issues = issues,
            Recommendations = recommendations,
            VerificationMethod = "automated",
            DurationSeconds = duration
        };
    }

    public async Task<ControlEvidenceDto> CollectEvidenceAsync(Guid controlId)
    {
        var evidence = new List<EvidenceItemDto>();

        // Authentication events
        var authEventCount = await _context.AuthenticationEvents.CountAsync();
        evidence.Add(new EvidenceItemDto
        {
            Type = "monitoring",
            Description = "Authentication events monitored",
            Value = authEventCount,
            Timestamp = DateTime.UtcNow,
            Source = "AuthenticationEvents table"
        });

        // Risk assessments
        var riskAssessmentCount = await _context.RiskAssessments.CountAsync();
        evidence.Add(new EvidenceItemDto
        {
            Type = "monitoring",
            Description = "Risk assessments performed",
            Value = riskAssessmentCount,
            Timestamp = DateTime.UtcNow,
            Source = "RiskAssessments table"
        });

        return new ControlEvidenceDto
        {
            ControlId = controlId,
            CollectedAt = DateTime.UtcNow,
            Items = evidence,
            TotalItems = evidence.Count,
            EvidenceTypeCounts = evidence.GroupBy(e => e.Type).ToDictionary(g => g.Key, g => g.Count())
        };
    }
}
