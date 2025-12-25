using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using USP.Core.Models.DTOs.Audit;
using USP.Core.Services.Audit;

namespace USP.Api.Controllers.Audit;

/// <summary>
/// Controller for audit log management and search
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class AuditController : ControllerBase
{
    private readonly IAuditService _auditService;
    private readonly ILogger<AuditController> _logger;

    public AuditController(
        IAuditService auditService,
        ILogger<AuditController> logger)
    {
        _auditService = auditService;
        _logger = logger;
    }

    /// <summary>
    /// Search audit logs with filtering and pagination
    /// </summary>
    [HttpGet("logs")]
    [ProducesResponseType(typeof(AuditSearchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AuditSearchResponse>> SearchLogs([FromQuery] AuditSearchRequest request)
    {
        try
        {
            var (logs, totalCount) = await _auditService.SearchAsync(request);

            var response = new AuditSearchResponse
            {
                Logs = logs,
                TotalCount = totalCount,
                Page = request.Page,
                PageSize = request.PageSize,
                TotalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize)
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching audit logs");
            return StatusCode(500, new { error = "Failed to search audit logs" });
        }
    }

    /// <summary>
    /// Get audit log by ID
    /// </summary>
    [HttpGet("logs/{id:guid}")]
    [ProducesResponseType(typeof(AuditLogDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AuditLogDto>> GetById(Guid id)
    {
        try
        {
            var log = await _auditService.GetByIdAsync(id);

            if (log == null)
                return NotFound(new { error = "Audit log not found" });

            return Ok(log);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving audit log {LogId}", id);
            return StatusCode(500, new { error = "Failed to retrieve audit log" });
        }
    }

    /// <summary>
    /// Export audit logs to file (CSV, JSON, PDF)
    /// </summary>
    [HttpPost("logs/export")]
    [ProducesResponseType(typeof(ExportResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ExportResponse>> ExportLogs([FromBody] AuditExportRequest request)
    {
        try
        {
            var filePath = await _auditService.ExportAsync(request);

            var response = new ExportResponse
            {
                FilePath = filePath,
                DownloadUrl = $"/api/v1/audit/logs/download?path={Uri.EscapeDataString(filePath)}",
                Message = "Audit logs exported successfully"
            };

            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting audit logs");
            return StatusCode(500, new { error = "Failed to export audit logs" });
        }
    }

    /// <summary>
    /// Download exported audit log file
    /// </summary>
    [HttpGet("logs/download")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult DownloadExport([FromQuery] string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path) || !global::System.IO.File.Exists(path))
                return NotFound(new { error = "File not found" });

            var fileBytes = global::System.IO.File.ReadAllBytes(path);
            var fileName = global::System.IO.Path.GetFileName(path);
            var contentType = global::System.IO.Path.GetExtension(path).ToLower() switch
            {
                ".csv" => "text/csv",
                ".json" => "application/json",
                ".pdf" => "application/pdf",
                _ => "application/octet-stream"
            };

            return File(fileBytes, contentType, fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading audit log export");
            return StatusCode(500, new { error = "Failed to download file" });
        }
    }

    /// <summary>
    /// Get audit log statistics
    /// </summary>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(AuditStatisticsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AuditStatisticsDto>> GetStatistics(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        try
        {
            var stats = await _auditService.GetStatisticsAsync(startDate, endDate);
            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving audit statistics");
            return StatusCode(500, new { error = "Failed to retrieve statistics" });
        }
    }

    /// <summary>
    /// Verify audit log chain integrity (tamper-proof check)
    /// </summary>
    [HttpPost("verify-integrity")]
    [ProducesResponseType(typeof(IntegrityCheckResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<IntegrityCheckResponse>> VerifyIntegrity(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        try
        {
            var isValid = await _auditService.VerifyChainIntegrityAsync(startDate, endDate);

            var response = new IntegrityCheckResponse
            {
                IsValid = isValid,
                Message = isValid
                    ? "Audit log chain integrity verified successfully"
                    : "Audit log chain integrity check FAILED - potential tampering detected",
                CheckedAt = DateTime.UtcNow,
                StartDate = startDate,
                EndDate = endDate
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying audit log integrity");
            return StatusCode(500, new { error = "Failed to verify integrity" });
        }
    }

    /// <summary>
    /// Trigger cleanup of old audit logs (admin only)
    /// </summary>
    [HttpPost("cleanup")]
    [Authorize(Roles = "admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> CleanupOldLogs([FromQuery] int retentionDays = 2555)
    {
        try
        {
            await _auditService.CleanupOldLogsAsync(retentionDays);

            return Ok(new
            {
                message = $"Cleanup completed for logs older than {retentionDays} days",
                retentionDays
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up old audit logs");
            return StatusCode(500, new { error = "Failed to cleanup logs" });
        }
    }
}

// Response DTOs
public class AuditSearchResponse
{
    public List<AuditLogDto> Logs { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}

public class ExportResponse
{
    public string FilePath { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class IntegrityCheckResponse
{
    public bool IsValid { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime CheckedAt { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}
