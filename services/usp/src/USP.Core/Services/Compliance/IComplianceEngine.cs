using USP.Core.Models.DTOs.Compliance;

namespace USP.Core.Services.Compliance;

/// <summary>
/// Service for compliance framework management and automated reporting
/// </summary>
public interface IComplianceEngine
{
    /// <summary>
    /// Generate compliance report for a specific framework
    /// </summary>
    Task<ComplianceReportDto> GenerateReportAsync(GenerateComplianceReportRequest request, Guid generatedBy);

    /// <summary>
    /// Get current compliance status for a framework
    /// </summary>
    Task<ComplianceStatusDto> GetComplianceStatusAsync(string framework);

    /// <summary>
    /// Get all supported compliance frameworks
    /// </summary>
    Task<List<string>> GetSupportedFrameworksAsync();

    /// <summary>
    /// Get list of compliance reports
    /// </summary>
    Task<List<ComplianceReportDto>> GetReportsAsync(string? framework = null, int page = 1, int pageSize = 20);

    /// <summary>
    /// Get compliance report by ID
    /// </summary>
    Task<ComplianceReportDto?> GetReportByIdAsync(Guid id);

    /// <summary>
    /// Download compliance report file
    /// </summary>
    Task<(byte[] FileData, string ContentType, string FileName)?> DownloadReportAsync(Guid id);

    /// <summary>
    /// Get control assessments for a framework
    /// </summary>
    Task<List<ControlAssessmentDto>> GetControlAssessmentsAsync(string framework);

    /// <summary>
    /// Update control assessment
    /// </summary>
    Task<bool> UpdateControlAssessmentAsync(Guid reportId, string controlId, string status, string? implementation = null, string? evidence = null, string? gaps = null);

    /// <summary>
    /// Get critical compliance gaps across all frameworks
    /// </summary>
    Task<List<ComplianceStatusDto>> GetCriticalGapsAsync();

    /// <summary>
    /// Schedule automated compliance report generation
    /// </summary>
    Task<bool> ScheduleAutomatedReportAsync(string framework, string schedule, string format = "PDF");
}
