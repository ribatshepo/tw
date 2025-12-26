using USP.Core.Models.DTOs.Compliance;

namespace USP.Core.Services.Compliance;

/// <summary>
/// SOC 2 Type II compliance service interface
/// </summary>
public interface ISoc2ComplianceService
{
    /// <summary>
    /// Assess SOC 2 Trust Service Criteria CC6.1 - Logical access controls
    /// </summary>
    Task<ControlAssessmentResult> AssessCC61Async(DateTime startDate, DateTime endDate);

    /// <summary>
    /// Assess SOC 2 Trust Service Criteria CC6.2 - Multi-factor authentication
    /// </summary>
    Task<ControlAssessmentResult> AssessCC62Async(DateTime startDate, DateTime endDate);

    /// <summary>
    /// Assess SOC 2 Trust Service Criteria CC6.3 - Privileged access management
    /// </summary>
    Task<ControlAssessmentResult> AssessCC63Async(DateTime startDate, DateTime endDate);

    /// <summary>
    /// Assess SOC 2 Trust Service Criteria CC6.6 - Encryption in transit and at rest
    /// </summary>
    Task<ControlAssessmentResult> AssessCC66Async(DateTime startDate, DateTime endDate);

    /// <summary>
    /// Assess SOC 2 Trust Service Criteria CC7.2 - System monitoring
    /// </summary>
    Task<ControlAssessmentResult> AssessCC72Async(DateTime startDate, DateTime endDate);

    /// <summary>
    /// Generate complete SOC 2 compliance report
    /// </summary>
    Task<ComplianceReportDto> GenerateReportAsync(DateTime startDate, DateTime endDate, Guid generatedBy);

    /// <summary>
    /// Collect evidence for SOC 2 audit
    /// </summary>
    Task<List<EvidenceDto>> CollectAuditEvidenceAsync(DateTime startDate, DateTime endDate);
}

/// <summary>
/// Control assessment result
/// </summary>
public class ControlAssessmentResult
{
    public string ControlId { get; set; } = string.Empty;
    public string ControlName { get; set; } = string.Empty;
    public string Status { get; set; } = "not_implemented"; // implemented, partial, not_implemented
    public double Score { get; set; } // 0-100
    public string? Implementation { get; set; }
    public string? Evidence { get; set; }
    public string? Gaps { get; set; }
    public Dictionary<string, object> Metrics { get; set; } = new();
}

/// <summary>
/// Evidence DTO
/// </summary>
public class EvidenceDto
{
    public Guid Id { get; set; }
    public string ControlId { get; set; } = string.Empty;
    public string EvidenceType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime CollectedAt { get; set; }
    public bool IsAutomated { get; set; }
}
