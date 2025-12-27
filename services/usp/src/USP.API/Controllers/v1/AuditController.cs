using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using USP.Core.Domain.Enums;
using USP.Core.Interfaces.Services.Audit;
using USP.Infrastructure.Authorization;

namespace USP.API.Controllers.v1;

/// <summary>
/// Audit logging and query API.
/// Provides comprehensive audit trail search, export, and integrity verification.
/// </summary>
[ApiController]
[Route("api/v1/audit")]
[Authorize]
[RequirePermission("audit:read")]
public class AuditController : ControllerBase
{
    private readonly IAuditService _auditService;
    private readonly ILogger<AuditController> _logger;

    public AuditController(
        IAuditService auditService,
        ILogger<AuditController> logger)
    {
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Search audit logs with advanced filtering and pagination.
    /// </summary>
    /// <param name="request">Search parameters</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Paginated audit log results</returns>
    [HttpPost("search")]
    [ProducesResponseType(typeof(AuditSearchResult), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> SearchAuditLogs(
        [FromBody] AuditSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _auditService.SearchAuditLogsAsync(request, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching audit logs");
            return BadRequest(new { error = "Failed to search audit logs" });
        }
    }

    /// <summary>
    /// Get a specific audit log entry by ID.
    /// </summary>
    /// <param name="id">Audit log ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Audit log entry</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(AuditLog), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetAuditLogById(
        string id,
        CancellationToken cancellationToken = default)
    {
        var auditLog = await _auditService.GetAuditLogByIdAsync(id, cancellationToken);

        if (auditLog == null)
        {
            return NotFound(new { error = "Audit log not found" });
        }

        return Ok(auditLog);
    }

    /// <summary>
    /// Get audit logs for a specific user.
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="startDate">Optional start date filter</param>
    /// <param name="endDate">Optional end date filter</param>
    /// <param name="limit">Maximum number of results (default 100)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of audit logs for the user</returns>
    [HttpGet("user/{userId}")]
    [ProducesResponseType(typeof(List<AuditLog>), 200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> GetUserAuditLogs(
        string userId,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var logs = await _auditService.GetUserAuditLogsAsync(
                userId,
                startDate,
                endDate,
                limit,
                cancellationToken);

            return Ok(logs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user audit logs for {UserId}", userId);
            return BadRequest(new { error = "Failed to retrieve user audit logs" });
        }
    }

    /// <summary>
    /// Get audit logs for a specific resource.
    /// </summary>
    /// <param name="resource">Resource path</param>
    /// <param name="startDate">Optional start date filter</param>
    /// <param name="endDate">Optional end date filter</param>
    /// <param name="limit">Maximum number of results (default 100)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of audit logs for the resource</returns>
    [HttpGet("resource")]
    [ProducesResponseType(typeof(List<AuditLog>), 200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> GetResourceAuditLogs(
        [FromQuery] string resource,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(resource))
        {
            return BadRequest(new { error = "Resource parameter is required" });
        }

        try
        {
            var logs = await _auditService.GetResourceAuditLogsAsync(
                resource,
                startDate,
                endDate,
                limit,
                cancellationToken);

            return Ok(logs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting resource audit logs for {Resource}", resource);
            return BadRequest(new { error = "Failed to retrieve resource audit logs" });
        }
    }

    /// <summary>
    /// Get audit logs by correlation ID (all logs for a single request).
    /// </summary>
    /// <param name="correlationId">Correlation ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of audit logs with the same correlation ID</returns>
    [HttpGet("correlation/{correlationId}")]
    [ProducesResponseType(typeof(List<AuditLog>), 200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> GetAuditLogsByCorrelationId(
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var logs = await _auditService.GetAuditLogsByCorrelationIdAsync(
                correlationId,
                cancellationToken);

            return Ok(logs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting audit logs for correlation ID {CorrelationId}", correlationId);
            return BadRequest(new { error = "Failed to retrieve audit logs" });
        }
    }

    /// <summary>
    /// Export audit logs to CSV format.
    /// </summary>
    /// <param name="request">Search parameters for logs to export</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>CSV file</returns>
    [HttpPost("export/csv")]
    [RequirePermission("audit:export")]
    [ProducesResponseType(typeof(FileContentResult), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> ExportToCsv(
        [FromBody] AuditSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var csvBytes = await _auditService.ExportAuditLogsToCsvAsync(request, cancellationToken);

            var fileName = $"audit-logs-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv";

            return File(csvBytes, "text/csv", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting audit logs to CSV");
            return BadRequest(new { error = "Failed to export audit logs" });
        }
    }

    /// <summary>
    /// Export audit logs to JSON format.
    /// </summary>
    /// <param name="request">Search parameters for logs to export</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>JSON file</returns>
    [HttpPost("export/json")]
    [RequirePermission("audit:export")]
    [ProducesResponseType(typeof(FileContentResult), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> ExportToJson(
        [FromBody] AuditSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var jsonBytes = await _auditService.ExportAuditLogsToJsonAsync(request, cancellationToken);

            var fileName = $"audit-logs-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json";

            return File(jsonBytes, "application/json", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting audit logs to JSON");
            return BadRequest(new { error = "Failed to export audit logs" });
        }
    }

    /// <summary>
    /// Verify the integrity of audit logs using hash chains.
    /// </summary>
    /// <param name="startDate">Optional start date for verification</param>
    /// <param name="endDate">Optional end date for verification</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Integrity verification result</returns>
    [HttpPost("verify-integrity")]
    [RequirePermission("audit:admin")]
    [ProducesResponseType(typeof(AuditIntegrityResult), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> VerifyIntegrity(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _auditService.VerifyAuditIntegrityAsync(
                startDate,
                endDate,
                cancellationToken);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying audit integrity");
            return BadRequest(new { error = "Failed to verify audit integrity" });
        }
    }

    /// <summary>
    /// Get audit statistics for a time period.
    /// </summary>
    /// <param name="startDate">Start date (required)</param>
    /// <param name="endDate">End date (required)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Audit statistics including event counts, top users, and top resources</returns>
    [HttpGet("statistics")]
    [RequirePermission("audit:admin")]
    [ProducesResponseType(typeof(AuditStatistics), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> GetStatistics(
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        if (startDate == default || endDate == default)
        {
            return BadRequest(new { error = "Both startDate and endDate are required" });
        }

        if (startDate > endDate)
        {
            return BadRequest(new { error = "startDate must be before endDate" });
        }

        try
        {
            var statistics = await _auditService.GetAuditStatisticsAsync(
                startDate,
                endDate,
                cancellationToken);

            return Ok(statistics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting audit statistics");
            return BadRequest(new { error = "Failed to retrieve audit statistics" });
        }
    }

    /// <summary>
    /// Delete audit logs older than the specified cutoff date.
    /// WARNING: This is a destructive operation and should only be used for compliance-based retention.
    /// </summary>
    /// <param name="cutoffDate">Delete logs older than this date</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of deleted audit logs</returns>
    [HttpDelete("retention")]
    [RequirePermission("audit:admin")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> DeleteOldLogs(
        [FromQuery] DateTime cutoffDate,
        CancellationToken cancellationToken = default)
    {
        if (cutoffDate == default)
        {
            return BadRequest(new { error = "cutoffDate is required" });
        }

        if (cutoffDate > DateTime.UtcNow.AddDays(-30))
        {
            return BadRequest(new
            {
                error = "Cutoff date must be at least 30 days in the past for safety"
            });
        }

        try
        {
            var deletedCount = await _auditService.DeleteAuditLogsOlderThanAsync(
                cutoffDate,
                cancellationToken);

            return Ok(new
            {
                deletedCount,
                cutoffDate,
                message = $"Deleted {deletedCount} audit log(s) older than {cutoffDate:yyyy-MM-dd}"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting old audit logs");
            return BadRequest(new { error = "Failed to delete old audit logs" });
        }
    }
}
