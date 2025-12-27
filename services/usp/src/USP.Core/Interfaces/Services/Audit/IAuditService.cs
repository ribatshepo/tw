using USP.Core.Domain.Entities.Audit;
using USP.Core.Domain.Enums;

namespace USP.Core.Interfaces.Services.Audit;

/// <summary>
/// Provides audit logging operations with tamper-proof storage, search, and export capabilities.
/// All audit logs are encrypted and include hash chains for integrity verification.
/// </summary>
public interface IAuditService
{
    /// <summary>
    /// Logs an audit event with full context information.
    /// </summary>
    /// <param name="eventType">Type of event being logged</param>
    /// <param name="userId">User ID who triggered the event (null for system events)</param>
    /// <param name="userName">Username for easier log reading</param>
    /// <param name="resource">Resource being accessed (e.g., "secrets/production/db")</param>
    /// <param name="action">Action performed (e.g., "read", "write", "delete")</param>
    /// <param name="success">Whether the operation succeeded</param>
    /// <param name="ipAddress">Client IP address</param>
    /// <param name="userAgent">Client user agent</param>
    /// <param name="details">Additional details as JSON or plain text</param>
    /// <param name="correlationId">Correlation ID for request tracing</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The created audit log entry</returns>
    Task<AuditLog> LogEventAsync(
        AuditEventType eventType,
        string? userId,
        string? userName,
        string? resource,
        string? action,
        bool success,
        string? ipAddress = null,
        string? userAgent = null,
        string? details = null,
        string? correlationId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Logs an authentication event (login, logout, MFA, password change).
    /// </summary>
    Task<AuditLog> LogAuthenticationEventAsync(
        AuditEventType eventType,
        string userId,
        string userName,
        bool success,
        string? ipAddress = null,
        string? userAgent = null,
        string? details = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Logs an authorization event (permission check, role assignment).
    /// </summary>
    Task<AuditLog> LogAuthorizationEventAsync(
        AuditEventType eventType,
        string userId,
        string resource,
        string action,
        bool granted,
        string? reason = null,
        string? ipAddress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Logs a secret management event (create, read, update, delete secret).
    /// </summary>
    Task<AuditLog> LogSecretEventAsync(
        AuditEventType eventType,
        string userId,
        string secretPath,
        bool success,
        string? details = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Logs a PAM event (checkout, checkin, session recording, JIT access).
    /// </summary>
    Task<AuditLog> LogPAMEventAsync(
        AuditEventType eventType,
        string userId,
        string accountId,
        bool success,
        string? details = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Logs a system event (vault seal/unseal, configuration change).
    /// </summary>
    Task<AuditLog> LogSystemEventAsync(
        AuditEventType eventType,
        string? details = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches audit logs with advanced filtering.
    /// </summary>
    /// <param name="request">Search parameters</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Search results with pagination</returns>
    Task<AuditSearchResult> SearchAuditLogsAsync(
        AuditSearchRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific audit log entry by ID.
    /// </summary>
    Task<AuditLog?> GetAuditLogByIdAsync(
        string id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets audit logs for a specific user.
    /// </summary>
    Task<List<AuditLog>> GetUserAuditLogsAsync(
        string userId,
        DateTime? startDate = null,
        DateTime? endDate = null,
        int limit = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets audit logs for a specific resource.
    /// </summary>
    Task<List<AuditLog>> GetResourceAuditLogsAsync(
        string resource,
        DateTime? startDate = null,
        DateTime? endDate = null,
        int limit = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets audit logs by correlation ID (all logs for a single request).
    /// </summary>
    Task<List<AuditLog>> GetAuditLogsByCorrelationIdAsync(
        string correlationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Exports audit logs to CSV format.
    /// </summary>
    Task<byte[]> ExportAuditLogsToCsvAsync(
        AuditSearchRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Exports audit logs to JSON format.
    /// </summary>
    Task<byte[]> ExportAuditLogsToJsonAsync(
        AuditSearchRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies the integrity of audit logs using hash chains.
    /// </summary>
    /// <param name="startDate">Start date for verification</param>
    /// <param name="endDate">End date for verification</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Integrity verification result</returns>
    Task<AuditIntegrityResult> VerifyAuditIntegrityAsync(
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets audit statistics for a time period.
    /// </summary>
    Task<AuditStatistics> GetAuditStatisticsAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes old audit logs (compliance-based retention).
    /// WARNING: This should only be called by automated retention policies.
    /// </summary>
    Task<int> DeleteAuditLogsOlderThanAsync(
        DateTime cutoffDate,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Search parameters for audit log queries
/// </summary>
public class AuditSearchRequest
{
    /// <summary>
    /// Filter by user ID
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// Filter by username
    /// </summary>
    public string? UserName { get; set; }

    /// <summary>
    /// Filter by event type
    /// </summary>
    public AuditEventType? EventType { get; set; }

    /// <summary>
    /// Filter by resource (supports wildcards)
    /// </summary>
    public string? Resource { get; set; }

    /// <summary>
    /// Filter by action
    /// </summary>
    public string? Action { get; set; }

    /// <summary>
    /// Filter by success/failure
    /// </summary>
    public bool? Success { get; set; }

    /// <summary>
    /// Filter by IP address
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// Filter by correlation ID
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// Start date for time range filter
    /// </summary>
    public DateTime? StartDate { get; set; }

    /// <summary>
    /// End date for time range filter
    /// </summary>
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// Search in details field (full-text search)
    /// </summary>
    public string? SearchText { get; set; }

    /// <summary>
    /// Page number (1-based)
    /// </summary>
    public int Page { get; set; } = 1;

    /// <summary>
    /// Page size (max 1000)
    /// </summary>
    public int PageSize { get; set; } = 100;

    /// <summary>
    /// Sort field
    /// </summary>
    public string SortBy { get; set; } = "Timestamp";

    /// <summary>
    /// Sort direction (asc or desc)
    /// </summary>
    public string SortDirection { get; set; } = "desc";
}

/// <summary>
/// Search result with pagination
/// </summary>
public class AuditSearchResult
{
    public required List<AuditLog> Results { get; set; }
    public required int TotalCount { get; set; }
    public required int Page { get; set; }
    public required int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
}

/// <summary>
/// Result of audit integrity verification
/// </summary>
public class AuditIntegrityResult
{
    public required bool IsValid { get; set; }
    public required int TotalRecords { get; set; }
    public required int VerifiedRecords { get; set; }
    public List<string> InvalidRecordIds { get; set; } = new();
    public string? Message { get; set; }
}

/// <summary>
/// Audit statistics for a time period
/// </summary>
public class AuditStatistics
{
    public required int TotalEvents { get; set; }
    public required int SuccessfulEvents { get; set; }
    public required int FailedEvents { get; set; }
    public required int UniqueUsers { get; set; }
    public required int UniqueResources { get; set; }
    public Dictionary<AuditEventType, int> EventTypeCounts { get; set; } = new();
    public Dictionary<string, int> TopUsers { get; set; } = new();
    public Dictionary<string, int> TopResources { get; set; } = new();
    public Dictionary<string, int> TopActions { get; set; } = new();
    public required DateTime StartDate { get; set; }
    public required DateTime EndDate { get; set; }
}
