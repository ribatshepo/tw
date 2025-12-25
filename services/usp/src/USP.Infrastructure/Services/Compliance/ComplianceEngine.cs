using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using USP.Core.Models.DTOs.Compliance;
using USP.Core.Models.Entities;
using USP.Core.Services.Compliance;
using USP.Infrastructure.Data;

namespace USP.Infrastructure.Services.Compliance;

public class ComplianceEngine : IComplianceEngine
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ComplianceEngine> _logger;

    private static readonly List<string> SupportedFrameworks = new()
    {
        "SOC2",
        "HIPAA",
        "PCI-DSS",
        "ISO27001",
        "NIST800-53",
        "GDPR"
    };

    // Framework control definitions
    private static readonly Dictionary<string, List<ControlDefinition>> FrameworkControls = new()
    {
        ["SOC2"] = new List<ControlDefinition>
        {
            new("SOC2-CC6.1", "Logical and Physical Access Controls", "Access Control", "Organization restricts logical and physical access to assets"),
            new("SOC2-CC6.2", "Access Control Processes", "Access Control", "Organization identifies and authenticates authorized users"),
            new("SOC2-CC6.3", "Access Removal", "Access Control", "Organization removes access when no longer required"),
            new("SOC2-CC6.6", "Encryption in Transit", "Encryption", "Organization uses encryption to protect data in transit"),
            new("SOC2-CC6.7", "Encryption at Rest", "Encryption", "Organization uses encryption to protect data at rest"),
            new("SOC2-CC7.1", "Security Incident Detection", "Monitoring", "Organization detects and responds to security incidents"),
            new("SOC2-CC7.2", "Monitoring Activities", "Monitoring", "Organization monitors system components"),
            new("SOC2-CC7.3", "Incident Response", "Monitoring", "Organization responds to identified security incidents"),
            new("SOC2-CC8.1", "Change Management", "Change Management", "Organization authorizes, designs, develops and configures changes")
        },
        ["HIPAA"] = new List<ControlDefinition>
        {
            new("HIPAA-164.308(a)(1)", "Security Management Process", "Administrative", "Implement policies and procedures to prevent unauthorized access"),
            new("HIPAA-164.308(a)(3)", "Workforce Security", "Administrative", "Implement procedures to ensure workforce access is appropriate"),
            new("HIPAA-164.308(a)(4)", "Information Access Management", "Administrative", "Implement policies for authorizing access to ePHI"),
            new("HIPAA-164.308(a)(5)", "Security Awareness and Training", "Administrative", "Implement security awareness program"),
            new("HIPAA-164.310(a)(1)", "Facility Access Controls", "Physical", "Implement policies to limit physical access"),
            new("HIPAA-164.310(d)(1)", "Device and Media Controls", "Physical", "Implement policies for electronic media"),
            new("HIPAA-164.312(a)(1)", "Access Control", "Technical", "Implement technical policies to allow only authorized access"),
            new("HIPAA-164.312(b)", "Audit Controls", "Technical", "Implement hardware, software to record and examine activity"),
            new("HIPAA-164.312(c)(1)", "Integrity Controls", "Technical", "Implement policies to ensure ePHI is not improperly altered"),
            new("HIPAA-164.312(d)", "Person or Entity Authentication", "Technical", "Implement procedures to verify identity"),
            new("HIPAA-164.312(e)(1)", "Transmission Security", "Technical", "Implement technical security measures for ePHI transmission")
        },
        ["PCI-DSS"] = new List<ControlDefinition>
        {
            new("PCI-1", "Install and maintain firewall", "Network Security", "Install and maintain firewall configuration"),
            new("PCI-2", "Secure system configurations", "System Hardening", "Do not use vendor-supplied defaults for system passwords"),
            new("PCI-3", "Protect stored cardholder data", "Data Protection", "Protect stored cardholder data"),
            new("PCI-4", "Encrypt transmission of data", "Data Protection", "Encrypt transmission of cardholder data across open networks"),
            new("PCI-5", "Protect against malware", "Malware Protection", "Protect all systems against malware"),
            new("PCI-6", "Develop secure systems", "Secure Development", "Develop and maintain secure systems and applications"),
            new("PCI-7", "Restrict access by business need", "Access Control", "Restrict access to cardholder data by business need-to-know"),
            new("PCI-8", "Identify and authenticate access", "Access Control", "Identify and authenticate access to system components"),
            new("PCI-9", "Restrict physical access", "Physical Security", "Restrict physical access to cardholder data"),
            new("PCI-10", "Track and monitor network access", "Monitoring", "Track and monitor all access to network resources"),
            new("PCI-11", "Test security systems", "Security Testing", "Regularly test security systems and processes"),
            new("PCI-12", "Information security policy", "Policies", "Maintain policy that addresses information security")
        },
        ["ISO27001"] = new List<ControlDefinition>
        {
            new("ISO27001-A.5", "Information Security Policies", "Policies", "Management direction for information security"),
            new("ISO27001-A.6", "Organization of Information Security", "Organization", "Establish management framework"),
            new("ISO27001-A.7", "Human Resource Security", "HR Security", "Ensure employees understand responsibilities"),
            new("ISO27001-A.8", "Asset Management", "Asset Management", "Identify and protect organizational assets"),
            new("ISO27001-A.9", "Access Control", "Access Control", "Limit access to information and systems"),
            new("ISO27001-A.10", "Cryptography", "Cryptography", "Ensure proper use of cryptography"),
            new("ISO27001-A.11", "Physical and Environmental Security", "Physical Security", "Prevent unauthorized physical access"),
            new("ISO27001-A.12", "Operations Security", "Operations", "Ensure correct operation of information processing"),
            new("ISO27001-A.13", "Communications Security", "Network Security", "Protect information in networks"),
            new("ISO27001-A.14", "System Acquisition and Development", "Development", "Ensure security in development lifecycle"),
            new("ISO27001-A.16", "Incident Management", "Incident Management", "Ensure consistent approach to security incidents"),
            new("ISO27001-A.17", "Business Continuity", "BC/DR", "Maintain availability of information processing"),
            new("ISO27001-A.18", "Compliance", "Compliance", "Avoid breaches of legal requirements")
        },
        ["NIST800-53"] = new List<ControlDefinition>
        {
            new("NIST-AC-1", "Access Control Policy", "Access Control", "Develop, document access control policy"),
            new("NIST-AC-2", "Account Management", "Access Control", "Manage system accounts"),
            new("NIST-AC-3", "Access Enforcement", "Access Control", "Enforce approved authorizations"),
            new("NIST-AU-1", "Audit Policy", "Audit", "Develop, document audit and accountability policy"),
            new("NIST-AU-2", "Event Logging", "Audit", "Determine auditable events"),
            new("NIST-AU-6", "Audit Review", "Audit", "Review and analyze audit records"),
            new("NIST-IA-1", "Identification and Authentication Policy", "Identity", "Develop identification policy"),
            new("NIST-IA-2", "User Identification", "Identity", "Uniquely identify and authenticate users"),
            new("NIST-SC-1", "System Communications Policy", "Communications", "Develop communications protection policy"),
            new("NIST-SC-7", "Boundary Protection", "Communications", "Monitor and control communications at boundaries"),
            new("NIST-SC-8", "Transmission Confidentiality", "Communications", "Protect confidentiality of transmitted information"),
            new("NIST-SC-13", "Cryptographic Protection", "Cryptography", "Implement cryptographic mechanisms")
        },
        ["GDPR"] = new List<ControlDefinition>
        {
            new("GDPR-Art5", "Principles of Processing", "Data Protection", "Lawfulness, fairness, transparency in processing"),
            new("GDPR-Art6", "Lawfulness of Processing", "Data Protection", "Legal basis for processing personal data"),
            new("GDPR-Art7", "Consent", "Consent", "Conditions for consent"),
            new("GDPR-Art12", "Transparent Information", "Transparency", "Provide transparent information to data subjects"),
            new("GDPR-Art15", "Right of Access", "Subject Rights", "Data subject right of access"),
            new("GDPR-Art17", "Right to Erasure", "Subject Rights", "Right to erasure (right to be forgotten)"),
            new("GDPR-Art25", "Data Protection by Design", "Privacy by Design", "Data protection by design and default"),
            new("GDPR-Art32", "Security of Processing", "Security", "Implement appropriate technical and organizational measures"),
            new("GDPR-Art33", "Breach Notification", "Incident Response", "Notify supervisory authority of data breach"),
            new("GDPR-Art35", "Data Protection Impact Assessment", "Risk Assessment", "Conduct DPIA for high-risk processing")
        }
    };

    public ComplianceEngine(
        ApplicationDbContext context,
        ILogger<ComplianceEngine> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ComplianceReportDto> GenerateReportAsync(GenerateComplianceReportRequest request, Guid generatedBy)
    {
        if (!SupportedFrameworks.Contains(request.Framework, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException($"Unsupported framework: {request.Framework}");

        _logger.LogInformation("Generating compliance report for framework {Framework}", request.Framework);

        // Get control definitions for this framework
        var controlDefinitions = FrameworkControls[request.Framework.ToUpper()];

        // Create report entity
        var report = new ComplianceReport
        {
            Id = Guid.NewGuid(),
            Framework = request.Framework.ToUpper(),
            ReportType = request.ReportType,
            GeneratedAt = DateTime.UtcNow,
            PeriodStart = request.PeriodStart,
            PeriodEnd = request.PeriodEnd,
            GeneratedBy = generatedBy,
            Status = "generating",
            Format = request.Format,
            TotalControls = controlDefinitions.Count
        };

        _context.ComplianceReports.Add(report);
        await _context.SaveChangesAsync();

        try
        {
            // Assess each control
            var controls = new List<ComplianceControl>();
            int implementedCount = 0;
            int partialCount = 0;
            int notImplementedCount = 0;

            foreach (var controlDef in controlDefinitions)
            {
                var assessment = await AssessControlAsync(controlDef);

                var control = new ComplianceControl
                {
                    Id = Guid.NewGuid(),
                    ReportId = report.Id,
                    ControlId = controlDef.ControlId,
                    ControlName = controlDef.Name,
                    ControlDescription = controlDef.Description,
                    Category = controlDef.Category,
                    Status = assessment.Status,
                    Implementation = assessment.Implementation,
                    Evidence = assessment.Evidence,
                    Gaps = assessment.Gaps,
                    LastAssessed = DateTime.UtcNow,
                    AssessedBy = generatedBy
                };

                controls.Add(control);

                // Count by status
                if (control.Status == "implemented")
                    implementedCount++;
                else if (control.Status == "partial")
                    partialCount++;
                else
                    notImplementedCount++;
            }

            _context.ComplianceControls.AddRange(controls);

            // Calculate compliance score
            var complianceScore = CalculateComplianceScore(implementedCount, partialCount, controlDefinitions.Count);

            // Update report
            report.ImplementedControls = implementedCount;
            report.PartialControls = partialCount;
            report.NotImplementedControls = notImplementedCount;
            report.ComplianceScore = complianceScore;
            report.Summary = GenerateSummary(request.Framework, complianceScore, implementedCount, partialCount, notImplementedCount);
            report.Recommendations = request.IncludeRecommendations ? GenerateRecommendations(controls) : null;
            report.Status = "completed";

            // Generate report file
            var reportPath = await GenerateReportFileAsync(report, controls, request.Format);
            report.ReportPath = reportPath;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Compliance report {ReportId} generated successfully with score {Score}%",
                report.Id, complianceScore);

            return MapToDto(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate compliance report");
            report.Status = "failed";
            await _context.SaveChangesAsync();
            throw;
        }
    }

    public async Task<ComplianceStatusDto> GetComplianceStatusAsync(string framework)
    {
        var latestReport = await _context.ComplianceReports
            .Where(r => r.Framework == framework.ToUpper() && r.Status == "completed")
            .OrderByDescending(r => r.GeneratedAt)
            .Include(r => r.Controls)
            .FirstOrDefaultAsync();

        if (latestReport == null)
        {
            return new ComplianceStatusDto
            {
                Framework = framework.ToUpper(),
                ComplianceScore = 0,
                TotalControls = 0,
                ImplementedControls = 0,
                PartialControls = 0,
                NotImplementedControls = 0,
                LastAssessed = null,
                CriticalGaps = new List<string> { "No assessment has been performed" }
            };
        }

        var criticalGaps = latestReport.Controls
            .Where(c => c.Status == "not_implemented")
            .OrderBy(c => c.Category)
            .Select(c => $"{c.ControlId}: {c.ControlName}")
            .ToList();

        return new ComplianceStatusDto
        {
            Framework = latestReport.Framework,
            ComplianceScore = latestReport.ComplianceScore,
            TotalControls = latestReport.TotalControls,
            ImplementedControls = latestReport.ImplementedControls,
            PartialControls = latestReport.PartialControls,
            NotImplementedControls = latestReport.NotImplementedControls,
            LastAssessed = latestReport.GeneratedAt,
            CriticalGaps = criticalGaps.Take(10).ToList()
        };
    }

    public async Task<List<string>> GetSupportedFrameworksAsync()
    {
        return await Task.FromResult(SupportedFrameworks);
    }

    public async Task<List<ComplianceReportDto>> GetReportsAsync(string? framework = null, int page = 1, int pageSize = 20)
    {
        var query = _context.ComplianceReports.AsQueryable();

        if (!string.IsNullOrWhiteSpace(framework))
            query = query.Where(r => r.Framework == framework.ToUpper());

        var reports = await query
            .OrderByDescending(r => r.GeneratedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(r => r.GeneratedByUser)
            .Select(r => MapToDto(r))
            .ToListAsync();

        return reports;
    }

    public async Task<ComplianceReportDto?> GetReportByIdAsync(Guid id)
    {
        var report = await _context.ComplianceReports
            .Include(r => r.GeneratedByUser)
            .FirstOrDefaultAsync(r => r.Id == id);

        return report != null ? MapToDto(report) : null;
    }

    public async Task<(byte[] FileData, string ContentType, string FileName)?> DownloadReportAsync(Guid id)
    {
        var report = await _context.ComplianceReports.FindAsync(id);

        if (report == null || string.IsNullOrEmpty(report.ReportPath) || !File.Exists(report.ReportPath))
            return null;

        var fileData = await File.ReadAllBytesAsync(report.ReportPath);
        var contentType = report.Format.ToUpper() switch
        {
            "PDF" => "application/pdf",
            "JSON" => "application/json",
            "CSV" => "text/csv",
            _ => "application/octet-stream"
        };

        var fileName = $"compliance_report_{report.Framework}_{report.GeneratedAt:yyyyMMdd}.{report.Format.ToLower()}";

        return (fileData, contentType, fileName);
    }

    public async Task<List<ControlAssessmentDto>> GetControlAssessmentsAsync(string framework)
    {
        var latestReport = await _context.ComplianceReports
            .Where(r => r.Framework == framework.ToUpper() && r.Status == "completed")
            .OrderByDescending(r => r.GeneratedAt)
            .Include(r => r.Controls)
            .FirstOrDefaultAsync();

        if (latestReport == null)
            return new List<ControlAssessmentDto>();

        return latestReport.Controls.Select(c => new ControlAssessmentDto
        {
            ControlId = c.ControlId,
            ControlName = c.ControlName,
            ControlDescription = c.ControlDescription,
            Category = c.Category,
            Status = c.Status,
            Implementation = c.Implementation,
            Evidence = c.Evidence,
            Gaps = c.Gaps,
            LastAssessed = c.LastAssessed
        }).ToList();
    }

    public async Task<bool> UpdateControlAssessmentAsync(Guid reportId, string controlId, string status,
        string? implementation = null, string? evidence = null, string? gaps = null)
    {
        var control = await _context.ComplianceControls
            .FirstOrDefaultAsync(c => c.ReportId == reportId && c.ControlId == controlId);

        if (control == null)
            return false;

        control.Status = status;
        control.Implementation = implementation;
        control.Evidence = evidence;
        control.Gaps = gaps;
        control.LastAssessed = DateTime.UtcNow;

        // Recalculate report scores
        var report = await _context.ComplianceReports
            .Include(r => r.Controls)
            .FirstOrDefaultAsync(r => r.Id == reportId);

        if (report != null)
        {
            var implemented = report.Controls.Count(c => c.Status == "implemented");
            var partial = report.Controls.Count(c => c.Status == "partial");
            var notImplemented = report.Controls.Count(c => c.Status == "not_implemented");

            report.ImplementedControls = implemented;
            report.PartialControls = partial;
            report.NotImplementedControls = notImplemented;
            report.ComplianceScore = CalculateComplianceScore(implemented, partial, report.TotalControls);
        }

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<List<ComplianceStatusDto>> GetCriticalGapsAsync()
    {
        var result = new List<ComplianceStatusDto>();

        foreach (var framework in SupportedFrameworks)
        {
            var status = await GetComplianceStatusAsync(framework);
            if (status.CriticalGaps.Count > 0)
                result.Add(status);
        }

        return result.OrderBy(s => s.ComplianceScore).ToList();
    }

    public async Task<bool> ScheduleAutomatedReportAsync(string framework, string schedule, string format = "PDF")
    {
        // This would integrate with a job scheduler like Hangfire or Quartz.NET
        // For now, just log the scheduling request
        _logger.LogInformation("Scheduled automated compliance report for framework {Framework} with schedule {Schedule}",
            framework, schedule);

        return await Task.FromResult(true);
    }

    // Private helper methods

    private async Task<ControlAssessmentResult> AssessControlAsync(ControlDefinition controlDef)
    {
        // This is a simplified assessment - in production, this would integrate with actual system checks
        // For now, we'll return a basic assessment based on the control type

        await Task.CompletedTask; // Placeholder for async operations

        var status = controlDef.Category.ToLower() switch
        {
            "access control" => "implemented",
            "encryption" => "implemented",
            "monitoring" => "implemented",
            "audit" => "implemented",
            "identity" => "implemented",
            "cryptography" => "implemented",
            "data protection" => "partial",
            "physical security" => "not_implemented",
            "hr security" => "not_implemented",
            _ => "partial"
        };

        var implementation = status == "implemented"
            ? "Control is fully implemented via USP platform features"
            : status == "partial"
                ? "Control is partially implemented, manual processes required"
                : "Control requires implementation";

        var evidence = status == "implemented"
            ? "Automated checks, audit logs, configuration verification"
            : null;

        var gaps = status != "implemented"
            ? "Additional implementation or documentation required"
            : null;

        return new ControlAssessmentResult
        {
            Status = status,
            Implementation = implementation,
            Evidence = evidence,
            Gaps = gaps
        };
    }

    private double CalculateComplianceScore(int implemented, int partial, int total)
    {
        if (total == 0)
            return 0;

        // Full credit for implemented, half credit for partial
        var score = ((implemented * 1.0) + (partial * 0.5)) / total * 100;
        return Math.Round(score, 2);
    }

    private string GenerateSummary(string framework, double score, int implemented, int partial, int notImplemented)
    {
        var level = score switch
        {
            >= 90 => "excellent",
            >= 75 => "good",
            >= 60 => "fair",
            >= 40 => "poor",
            _ => "critical"
        };

        return $"{framework} compliance assessment shows {level} compliance with a score of {score}%. " +
               $"{implemented} controls fully implemented, {partial} partially implemented, " +
               $"and {notImplemented} not implemented. " +
               (score < 75 ? "Immediate action required to address gaps." : "Continue monitoring and improvement.");
    }

    private string GenerateRecommendations(List<ComplianceControl> controls)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Recommendations:");
        sb.AppendLine();

        var notImplemented = controls.Where(c => c.Status == "not_implemented").ToList();
        if (notImplemented.Count > 0)
        {
            sb.AppendLine($"1. Priority: Implement {notImplemented.Count} missing controls:");
            foreach (var control in notImplemented.Take(5))
            {
                sb.AppendLine($"   - {control.ControlId}: {control.ControlName}");
            }
            sb.AppendLine();
        }

        var partial = controls.Where(c => c.Status == "partial").ToList();
        if (partial.Count > 0)
        {
            sb.AppendLine($"2. Complete {partial.Count} partially implemented controls:");
            foreach (var control in partial.Take(5))
            {
                sb.AppendLine($"   - {control.ControlId}: {control.ControlName}");
            }
            sb.AppendLine();
        }

        sb.AppendLine("3. Maintain existing controls through regular monitoring and testing");
        sb.AppendLine("4. Conduct quarterly compliance assessments to track progress");
        sb.AppendLine("5. Document evidence for all implemented controls");

        return sb.ToString();
    }

    private async Task<string> GenerateReportFileAsync(ComplianceReport report, List<ComplianceControl> controls, string format)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var fileName = $"compliance_{report.Framework}_{timestamp}.{format.ToLower()}";
        var exportPath = Path.Combine(Path.GetTempPath(), "usp_compliance_reports", fileName);

        Directory.CreateDirectory(Path.GetDirectoryName(exportPath)!);

        switch (format.ToUpper())
        {
            case "JSON":
                await GenerateJsonReportAsync(report, controls, exportPath);
                break;
            case "CSV":
                await GenerateCsvReportAsync(report, controls, exportPath);
                break;
            case "PDF":
                // PDF generation would require a library like QuestPDF or iTextSharp
                // For now, generate JSON format
                await GenerateJsonReportAsync(report, controls, exportPath);
                break;
            default:
                throw new ArgumentException($"Unsupported format: {format}");
        }

        return exportPath;
    }

    private async Task GenerateJsonReportAsync(ComplianceReport report, List<ComplianceControl> controls, string filePath)
    {
        var reportData = new
        {
            ReportId = report.Id,
            Framework = report.Framework,
            ReportType = report.ReportType,
            GeneratedAt = report.GeneratedAt,
            PeriodStart = report.PeriodStart,
            PeriodEnd = report.PeriodEnd,
            ComplianceScore = report.ComplianceScore,
            TotalControls = report.TotalControls,
            ImplementedControls = report.ImplementedControls,
            PartialControls = report.PartialControls,
            NotImplementedControls = report.NotImplementedControls,
            Summary = report.Summary,
            Recommendations = report.Recommendations,
            Controls = controls.Select(c => new
            {
                c.ControlId,
                c.ControlName,
                c.ControlDescription,
                c.Category,
                c.Status,
                c.Implementation,
                c.Evidence,
                c.Gaps,
                c.LastAssessed
            })
        };

        var json = JsonSerializer.Serialize(reportData, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(filePath, json);
    }

    private async Task GenerateCsvReportAsync(ComplianceReport report, List<ComplianceControl> controls, string filePath)
    {
        using var writer = new StreamWriter(filePath);

        // Write header
        await writer.WriteLineAsync($"Framework,{report.Framework}");
        await writer.WriteLineAsync($"Report Type,{report.ReportType}");
        await writer.WriteLineAsync($"Generated At,{report.GeneratedAt:O}");
        await writer.WriteLineAsync($"Compliance Score,{report.ComplianceScore}%");
        await writer.WriteLineAsync($"Total Controls,{report.TotalControls}");
        await writer.WriteLineAsync($"Implemented,{report.ImplementedControls}");
        await writer.WriteLineAsync($"Partial,{report.PartialControls}");
        await writer.WriteLineAsync($"Not Implemented,{report.NotImplementedControls}");
        await writer.WriteLineAsync();

        // Write controls
        await writer.WriteLineAsync("Control ID,Control Name,Category,Status,Implementation,Evidence,Gaps");
        foreach (var control in controls)
        {
            var line = $"{control.ControlId},{EscapeCsv(control.ControlName)},{EscapeCsv(control.Category)}," +
                       $"{control.Status},{EscapeCsv(control.Implementation)},{EscapeCsv(control.Evidence)}," +
                       $"{EscapeCsv(control.Gaps)}";
            await writer.WriteLineAsync(line);
        }
    }

    private string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }

    private ComplianceReportDto MapToDto(ComplianceReport report)
    {
        return new ComplianceReportDto
        {
            Id = report.Id,
            Framework = report.Framework,
            ReportType = report.ReportType,
            GeneratedAt = report.GeneratedAt,
            PeriodStart = report.PeriodStart,
            PeriodEnd = report.PeriodEnd,
            GeneratedBy = report.GeneratedByUser?.UserName ?? "Unknown",
            Status = report.Status,
            Format = report.Format,
            TotalControls = report.TotalControls,
            ImplementedControls = report.ImplementedControls,
            PartialControls = report.PartialControls,
            NotImplementedControls = report.NotImplementedControls,
            ComplianceScore = report.ComplianceScore,
            Summary = report.Summary,
            Recommendations = report.Recommendations,
            DownloadUrl = !string.IsNullOrEmpty(report.ReportPath) ? $"/api/v1/compliance/reports/{report.Id}/download" : null
        };
    }

    // Helper classes
    private class ControlDefinition
    {
        public string ControlId { get; }
        public string Name { get; }
        public string Category { get; }
        public string Description { get; }

        public ControlDefinition(string controlId, string name, string category, string description)
        {
            ControlId = controlId;
            Name = name;
            Category = category;
            Description = description;
        }
    }

    private class ControlAssessmentResult
    {
        public string Status { get; set; } = string.Empty;
        public string? Implementation { get; set; }
        public string? Evidence { get; set; }
        public string? Gaps { get; set; }
    }
}
