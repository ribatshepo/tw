using USP.Core.Models.DTOs.Compliance;

namespace USP.Core.Services.Compliance;

/// <summary>
/// Automated compliance verification and reporting service
/// Performs continuous compliance monitoring and evidence collection
/// </summary>
public interface IComplianceAutomationService
{
    /// <summary>
    /// Verify a specific compliance control
    /// Runs automated checks and collects evidence
    /// </summary>
    /// <param name="controlId">Control to verify</param>
    /// <param name="userId">User performing verification (null for automated)</param>
    /// <returns>Verification result with evidence and findings</returns>
    Task<ControlVerificationResultDto> VerifyControlAsync(Guid controlId, Guid? userId = null);

    /// <summary>
    /// Verify all controls for a specific framework
    /// </summary>
    /// <param name="frameworkName">Framework name (SOC2, HIPAA, PCI-DSS, etc.)</param>
    /// <param name="userId">User performing verification</param>
    /// <returns>List of verification results</returns>
    Task<List<ControlVerificationResultDto>> VerifyFrameworkAsync(string frameworkName, Guid? userId = null);

    /// <summary>
    /// Collect evidence for a control
    /// Gathers audit logs, configuration data, and other proof
    /// </summary>
    /// <param name="controlId">Control ID</param>
    /// <returns>Collected evidence</returns>
    Task<ControlEvidenceDto> CollectEvidenceAsync(Guid controlId);

    /// <summary>
    /// Generate automated compliance report
    /// </summary>
    /// <param name="frameworkName">Framework to report on</param>
    /// <param name="startDate">Report period start</param>
    /// <param name="endDate">Report period end</param>
    /// <param name="format">Report format (pdf, html, json, csv)</param>
    /// <returns>Generated report</returns>
    Task<AutomatedComplianceReportDto> GenerateAutomatedReportAsync(
        string frameworkName,
        DateTime? startDate = null,
        DateTime? endDate = null,
        string format = "json");

    /// <summary>
    /// Run continuous compliance check (background job)
    /// Verifies all scheduled controls
    /// </summary>
    /// <returns>Summary of verification run</returns>
    Task<ComplianceCheckSummaryDto> RunContinuousComplianceCheckAsync();

    /// <summary>
    /// Create remediation task for a compliance issue
    /// </summary>
    /// <param name="request">Remediation task details</param>
    /// <returns>Created task</returns>
    Task<RemediationTaskDto> CreateRemediationTaskAsync(CreateRemediationTaskDto request);

    /// <summary>
    /// Update remediation task status
    /// </summary>
    /// <param name="taskId">Task ID</param>
    /// <param name="status">New status</param>
    /// <param name="userId">User updating status</param>
    /// <param name="notes">Optional notes</param>
    /// <returns>Updated task</returns>
    Task<RemediationTaskDto> UpdateRemediationTaskStatusAsync(
        Guid taskId,
        string status,
        Guid userId,
        string? notes = null);

    /// <summary>
    /// Get remediation tasks for a control
    /// </summary>
    /// <param name="controlId">Control ID</param>
    /// <param name="status">Filter by status (optional)</param>
    /// <returns>List of remediation tasks</returns>
    Task<List<RemediationTaskDto>> GetRemediationTasksAsync(Guid controlId, string? status = null);

    /// <summary>
    /// Get verification history for a control
    /// </summary>
    /// <param name="controlId">Control ID</param>
    /// <param name="limit">Maximum results</param>
    /// <returns>Verification history</returns>
    Task<List<ControlVerificationResultDto>> GetVerificationHistoryAsync(Guid controlId, int limit = 10);

    /// <summary>
    /// Get compliance dashboard summary
    /// </summary>
    /// <param name="frameworkName">Framework name (optional)</param>
    /// <returns>Dashboard summary with metrics</returns>
    Task<ComplianceDashboardDto> GetComplianceDashboardAsync(string? frameworkName = null);

    /// <summary>
    /// Create or update verification schedule
    /// </summary>
    /// <param name="request">Schedule configuration</param>
    /// <returns>Created/updated schedule</returns>
    Task<VerificationScheduleDto> CreateOrUpdateScheduleAsync(CreateVerificationScheduleDto request);

    /// <summary>
    /// Get verification schedule for a control
    /// </summary>
    /// <param name="controlId">Control ID</param>
    /// <returns>Schedule details</returns>
    Task<VerificationScheduleDto?> GetScheduleAsync(Guid controlId);

    /// <summary>
    /// Delete verification schedule
    /// </summary>
    /// <param name="controlId">Control ID</param>
    Task DeleteScheduleAsync(Guid controlId);

    /// <summary>
    /// Get compliance trend data
    /// Shows compliance scores over time
    /// </summary>
    /// <param name="frameworkName">Framework name</param>
    /// <param name="days">Number of days to include</param>
    /// <returns>Trend data</returns>
    Task<ComplianceTrendDto> GetComplianceTrendAsync(string frameworkName, int days = 30);
}
