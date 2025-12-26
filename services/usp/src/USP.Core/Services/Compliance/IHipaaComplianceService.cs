using USP.Core.Models.DTOs.Compliance;

namespace USP.Core.Services.Compliance;

/// <summary>
/// HIPAA Security Rule compliance service interface
/// Implements requirements for Protected Health Information (PHI) security
/// </summary>
public interface IHipaaComplianceService
{
    /// <summary>
    /// Assess HIPAA §164.308(a)(3) - Workforce clearance procedure
    /// </summary>
    Task<ControlAssessmentResult> Assess164_308_a3_Async(DateTime startDate, DateTime endDate);

    /// <summary>
    /// Assess HIPAA §164.308(a)(4) - Information access management
    /// </summary>
    Task<ControlAssessmentResult> Assess164_308_a4_Async(DateTime startDate, DateTime endDate);

    /// <summary>
    /// Assess HIPAA §164.312(a)(1) - Unique user identification
    /// </summary>
    Task<ControlAssessmentResult> Assess164_312_a1_Async(DateTime startDate, DateTime endDate);

    /// <summary>
    /// Assess HIPAA §164.312(a)(2)(i) - Emergency access procedure
    /// </summary>
    Task<ControlAssessmentResult> Assess164_312_a2i_Async(DateTime startDate, DateTime endDate);

    /// <summary>
    /// Assess HIPAA §164.312(d) - Person or entity authentication
    /// </summary>
    Task<ControlAssessmentResult> Assess164_312_d_Async(DateTime startDate, DateTime endDate);

    /// <summary>
    /// Generate complete HIPAA compliance report
    /// </summary>
    Task<ComplianceReportDto> GenerateReportAsync(DateTime startDate, DateTime endDate, Guid generatedBy);

    /// <summary>
    /// Track PHI access for audit trail
    /// </summary>
    Task<List<PhiAccessRecord>> GetPhiAccessLogAsync(DateTime startDate, DateTime endDate);

    /// <summary>
    /// Verify Business Associate Agreement (BAA) compliance
    /// </summary>
    Task<BaaComplianceStatus> VerifyBaaComplianceAsync();
}

/// <summary>
/// PHI (Protected Health Information) access record
/// </summary>
public class PhiAccessRecord
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public string? ResourceId { get; set; }
    public DateTime AccessedAt { get; set; }
    public string? IpAddress { get; set; }
    public string Purpose { get; set; } = string.Empty; // Treatment, Payment, Operations
}

/// <summary>
/// Business Associate Agreement compliance status
/// </summary>
public class BaaComplianceStatus
{
    public bool IsCompliant { get; set; }
    public int TotalAgreements { get; set; }
    public int ActiveAgreements { get; set; }
    public int ExpiringSoon { get; set; }
    public int ExpiredAgreements { get; set; }
    public double CompliancePercentage { get; set; }
    public List<string> Issues { get; set; } = new();
}
