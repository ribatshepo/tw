using USP.Core.Models.DTOs.Audit;

namespace USP.Core.Services.Audit;

/// <summary>
/// Service for audit logging with tamper-proof chain and correlation tracking
/// </summary>
public interface IAuditService
{
    /// <summary>
    /// Log an audit event with automatic correlation ID and sensitive data redaction
    /// </summary>
    Task LogAsync(
        Guid? userId,
        string action,
        string resourceType,
        string? resourceId = null,
        object? oldValue = null,
        object? newValue = null,
        string? ipAddress = null,
        string? userAgent = null,
        string status = "success",
        string? errorMessage = null,
        string? correlationId = null);

    /// <summary>
    /// Search audit logs with filtering and pagination
    /// </summary>
    Task<(List<AuditLogDto> Logs, int TotalCount)> SearchAsync(AuditSearchRequest request);

    /// <summary>
    /// Get audit log by ID
    /// </summary>
    Task<AuditLogDto?> GetByIdAsync(Guid id);

    /// <summary>
    /// Export audit logs to file
    /// </summary>
    /// <returns>File path of exported file</returns>
    Task<string> ExportAsync(AuditExportRequest request);

    /// <summary>
    /// Get audit statistics
    /// </summary>
    Task<AuditStatisticsDto> GetStatisticsAsync(DateTime? startDate = null, DateTime? endDate = null);

    /// <summary>
    /// Verify audit log chain integrity (tamper-proof check)
    /// </summary>
    Task<bool> VerifyChainIntegrityAsync(DateTime? startDate = null, DateTime? endDate = null);

    /// <summary>
    /// Get correlation ID for current request (from HttpContext or generate new)
    /// </summary>
    string GetCorrelationId();

    /// <summary>
    /// Cleanup old audit logs based on retention policy
    /// </summary>
    Task CleanupOldLogsAsync(int retentionDays = 2555); // Default: 7 years
}
