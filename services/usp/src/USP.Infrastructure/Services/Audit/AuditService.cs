using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using USP.Core.Models.DTOs.Audit;
using USP.Core.Models.Entities;
using USP.Core.Services.Audit;
using USP.Infrastructure.Data;

namespace USP.Infrastructure.Services.Audit;

public class AuditService : IAuditService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AuditService> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;

    private const string CorrelationIdHeader = "X-Correlation-ID";
    private const string PreviousHashCacheKey = "audit:previous_hash";

    // Sensitive data patterns for redaction
    private static readonly HashSet<string> SensitiveFieldNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "secret", "token", "apikey", "api_key", "accesstoken", "access_token",
        "refreshtoken", "refresh_token", "privatekey", "private_key", "credential",
        "ssn", "social_security", "credit_card", "creditcard", "cvv", "pin"
    };

    public AuditService(
        ApplicationDbContext context,
        ILogger<AuditService> logger,
        IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task LogAsync(
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
        string? correlationId = null)
    {
        try
        {
            // Redact sensitive data
            var redactedOldValue = RedactSensitiveData(oldValue);
            var redactedNewValue = RedactSensitiveData(newValue);

            // Get correlation ID
            correlationId ??= GetCorrelationId();

            // Get previous hash for tamper-proof chain
            var previousHash = await GetPreviousHashAsync();

            // Create audit log entry
            var auditLog = new AuditLog
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Action = action,
                ResourceType = resourceType,
                ResourceId = resourceId,
                OldValue = redactedOldValue != null ? JsonSerializer.Serialize(redactedOldValue) : null,
                NewValue = redactedNewValue != null ? JsonSerializer.Serialize(redactedNewValue) : null,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                Status = status,
                ErrorMessage = errorMessage,
                CreatedAt = DateTime.UtcNow,
                CorrelationId = correlationId,
                PreviousHash = previousHash
            };

            // Calculate current hash (tamper-proof chain)
            auditLog.CurrentHash = CalculateHash(auditLog);

            // Save to database
            _context.AuditLogs.Add(auditLog);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Audit log created: {Action} on {ResourceType}/{ResourceId} by user {UserId} with status {Status}",
                action, resourceType, resourceId, userId, status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create audit log for action {Action}", action);
            // Do not throw - audit logging should not break the application
        }
    }

    public async Task<(List<AuditLogDto> Logs, int TotalCount)> SearchAsync(AuditSearchRequest request)
    {
        var query = _context.AuditLogs.AsQueryable();

        // Apply filters
        if (request.UserId.HasValue)
            query = query.Where(a => a.UserId == request.UserId.Value);

        if (!string.IsNullOrWhiteSpace(request.Action))
            query = query.Where(a => a.Action == request.Action);

        if (!string.IsNullOrWhiteSpace(request.ResourceType))
            query = query.Where(a => a.ResourceType == request.ResourceType);

        if (!string.IsNullOrWhiteSpace(request.ResourceId))
            query = query.Where(a => a.ResourceId == request.ResourceId);

        if (!string.IsNullOrWhiteSpace(request.Status))
            query = query.Where(a => a.Status == request.Status);

        if (request.StartDate.HasValue)
            query = query.Where(a => a.CreatedAt >= request.StartDate.Value);

        if (request.EndDate.HasValue)
            query = query.Where(a => a.CreatedAt <= request.EndDate.Value);

        if (!string.IsNullOrWhiteSpace(request.IpAddress))
            query = query.Where(a => a.IpAddress == request.IpAddress);

        // Full-text search
        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var searchTerm = request.SearchTerm.ToLower();
            query = query.Where(a =>
                a.Action.ToLower().Contains(searchTerm) ||
                a.ResourceType.ToLower().Contains(searchTerm) ||
                (a.ResourceId != null && a.ResourceId.ToLower().Contains(searchTerm)) ||
                (a.ErrorMessage != null && a.ErrorMessage.ToLower().Contains(searchTerm)));
        }

        // Get total count
        var totalCount = await query.CountAsync();

        // Pagination - get raw data first
        var rawLogs = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Include(a => a.User)
            .ToListAsync();

        // Map to DTOs and deserialize JSON
        var logs = rawLogs.Select(a => new AuditLogDto
        {
            Id = a.Id,
            UserId = a.UserId,
            UserName = a.User?.UserName,
            Action = a.Action,
            ResourceType = a.ResourceType,
            ResourceId = a.ResourceId,
            OldValue = a.OldValue != null ? JsonSerializer.Deserialize<object>(a.OldValue) : null,
            NewValue = a.NewValue != null ? JsonSerializer.Deserialize<object>(a.NewValue) : null,
            IpAddress = a.IpAddress,
            UserAgent = a.UserAgent,
            Status = a.Status,
            ErrorMessage = a.ErrorMessage,
            CreatedAt = a.CreatedAt,
            CorrelationId = a.CorrelationId
        }).ToList();

        return (logs, totalCount);
    }

    public async Task<AuditLogDto?> GetByIdAsync(Guid id)
    {
        var auditLog = await _context.AuditLogs
            .Include(a => a.User)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (auditLog == null)
            return null;

        return new AuditLogDto
        {
            Id = auditLog.Id,
            UserId = auditLog.UserId,
            UserName = auditLog.User?.UserName,
            Action = auditLog.Action,
            ResourceType = auditLog.ResourceType,
            ResourceId = auditLog.ResourceId,
            OldValue = auditLog.OldValue != null ? JsonSerializer.Deserialize<object>(auditLog.OldValue) : null,
            NewValue = auditLog.NewValue != null ? JsonSerializer.Deserialize<object>(auditLog.NewValue) : null,
            IpAddress = auditLog.IpAddress,
            UserAgent = auditLog.UserAgent,
            Status = auditLog.Status,
            ErrorMessage = auditLog.ErrorMessage,
            CreatedAt = auditLog.CreatedAt,
            CorrelationId = auditLog.CorrelationId
        };
    }

    public async Task<string> ExportAsync(AuditExportRequest request)
    {
        var (logs, _) = await SearchAsync(request.SearchCriteria);

        // If not including sensitive data, redact it
        if (!request.IncludeSensitiveData)
        {
            logs = logs.Select(log => new AuditLogDto
            {
                Id = log.Id,
                UserId = log.UserId,
                UserName = log.UserName,
                Action = log.Action,
                ResourceType = log.ResourceType,
                ResourceId = log.ResourceId,
                OldValue = RedactSensitiveData(log.OldValue),
                NewValue = RedactSensitiveData(log.NewValue),
                IpAddress = log.IpAddress,
                UserAgent = log.UserAgent,
                Status = log.Status,
                ErrorMessage = log.ErrorMessage,
                CreatedAt = log.CreatedAt,
                CorrelationId = log.CorrelationId
            }).ToList();
        }

        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var fileName = $"audit_logs_{timestamp}.{request.Format.ToLower()}";
        var exportPath = Path.Combine(Path.GetTempPath(), "usp_exports", fileName);

        // Create directory if it doesn't exist
        Directory.CreateDirectory(Path.GetDirectoryName(exportPath)!);

        switch (request.Format.ToUpper())
        {
            case "CSV":
                await ExportToCsvAsync(logs, exportPath);
                break;
            case "JSON":
                await ExportToJsonAsync(logs, exportPath);
                break;
            case "PDF":
                // PDF export would require a library like iTextSharp or QuestPDF
                // For now, export as JSON and return path
                await ExportToJsonAsync(logs, exportPath);
                break;
            default:
                throw new ArgumentException($"Unsupported export format: {request.Format}");
        }

        _logger.LogInformation("Exported {Count} audit logs to {FilePath}", logs.Count, exportPath);

        return exportPath;
    }

    public async Task<AuditStatisticsDto> GetStatisticsAsync(DateTime? startDate = null, DateTime? endDate = null)
    {
        var query = _context.AuditLogs.AsQueryable();

        if (startDate.HasValue)
            query = query.Where(a => a.CreatedAt >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(a => a.CreatedAt <= endDate.Value);

        var totalLogs = await query.CountAsync();
        var todayLogs = await query.Where(a => a.CreatedAt >= DateTime.UtcNow.Date).CountAsync();
        var failedActions = await query.Where(a => a.Status == "failed" || a.Status == "error").CountAsync();

        // Action breakdown
        var actionBreakdown = await query
            .GroupBy(a => a.Action)
            .Select(g => new { Action = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Action, x => x.Count);

        // User breakdown (top 10 users)
        var userBreakdown = await query
            .Where(a => a.UserId != null)
            .GroupBy(a => a.UserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(10)
            .ToDictionaryAsync(x => x.UserId.ToString()!, x => x.Count);

        // Top resources
        var topResources = await query
            .GroupBy(a => a.ResourceType)
            .Select(g => new TopResource { ResourceType = g.Key, Count = g.Count() })
            .OrderByDescending(r => r.Count)
            .Take(10)
            .ToListAsync();

        return new AuditStatisticsDto
        {
            TotalLogs = totalLogs,
            TodayLogs = todayLogs,
            FailedActions = failedActions,
            ActionBreakdown = actionBreakdown,
            UserBreakdown = userBreakdown,
            TopResources = topResources
        };
    }

    public async Task<bool> VerifyChainIntegrityAsync(DateTime? startDate = null, DateTime? endDate = null)
    {
        var query = _context.AuditLogs.AsQueryable();

        if (startDate.HasValue)
            query = query.Where(a => a.CreatedAt >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(a => a.CreatedAt <= endDate.Value);

        var logs = await query
            .OrderBy(a => a.CreatedAt)
            .ToListAsync();

        if (logs.Count == 0)
            return true;

        // Verify each log's hash
        for (int i = 0; i < logs.Count; i++)
        {
            var log = logs[i];

            // Recalculate hash
            var calculatedHash = CalculateHash(log);

            if (log.CurrentHash != calculatedHash)
            {
                _logger.LogWarning("Audit log chain integrity check failed at log {LogId}. Expected hash {Expected}, got {Actual}",
                    log.Id, log.CurrentHash, calculatedHash);
                return false;
            }

            // Verify chain linkage (except for first log)
            if (i > 0)
            {
                var previousLog = logs[i - 1];
                if (log.PreviousHash != previousLog.CurrentHash)
                {
                    _logger.LogWarning("Audit log chain linkage failed at log {LogId}. Previous hash mismatch.",
                        log.Id);
                    return false;
                }
            }
        }

        _logger.LogInformation("Audit log chain integrity verified for {Count} logs", logs.Count);
        return true;
    }

    public string GetCorrelationId()
    {
        // Try to get from HTTP context
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext != null)
        {
            if (httpContext.Request.Headers.TryGetValue(CorrelationIdHeader, out var correlationId))
                return correlationId.ToString();

            // Check if it's already set in response headers
            if (httpContext.Response.Headers.TryGetValue(CorrelationIdHeader, out var existingId))
                return existingId.ToString();
        }

        // Generate new correlation ID
        var newCorrelationId = Guid.NewGuid().ToString();

        // Set it in response headers if we have HTTP context
        if (httpContext != null)
        {
            httpContext.Response.Headers.Append(CorrelationIdHeader, newCorrelationId);
        }

        return newCorrelationId;
    }

    public async Task CleanupOldLogsAsync(int retentionDays = 2555)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);

        var oldLogs = await _context.AuditLogs
            .Where(a => a.CreatedAt < cutoffDate)
            .ToListAsync();

        if (oldLogs.Count > 0)
        {
            _context.AuditLogs.RemoveRange(oldLogs);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Cleaned up {Count} audit logs older than {RetentionDays} days",
                oldLogs.Count, retentionDays);
        }
    }

    // Private helper methods

    private object? RedactSensitiveData(object? data)
    {
        if (data == null)
            return null;

        // If it's a simple type, return as-is
        if (data.GetType().IsPrimitive || data is string || data is DateTime)
            return data;

        try
        {
            // Serialize to JSON and redact
            var json = JsonSerializer.Serialize(data);
            var jsonDoc = JsonDocument.Parse(json);
            var redacted = RedactJsonElement(jsonDoc.RootElement);
            return JsonSerializer.Deserialize<object>(redacted);
        }
        catch
        {
            return data;
        }
    }

    private string RedactJsonElement(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var sb = new StringBuilder();
            sb.Append('{');
            bool first = true;

            foreach (var property in element.EnumerateObject())
            {
                if (!first) sb.Append(',');
                first = false;

                sb.Append($"\"{property.Name}\":");

                // Check if property name is sensitive
                if (SensitiveFieldNames.Contains(property.Name))
                {
                    sb.Append("\"[REDACTED]\"");
                }
                else
                {
                    sb.Append(RedactJsonElement(property.Value));
                }
            }

            sb.Append('}');
            return sb.ToString();
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            var sb = new StringBuilder();
            sb.Append('[');
            bool first = true;

            foreach (var item in element.EnumerateArray())
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append(RedactJsonElement(item));
            }

            sb.Append(']');
            return sb.ToString();
        }
        else
        {
            return element.GetRawText();
        }
    }

    private string CalculateHash(AuditLog log)
    {
        var data = $"{log.Id}|{log.UserId}|{log.Action}|{log.ResourceType}|{log.ResourceId}|" +
                   $"{log.OldValue}|{log.NewValue}|{log.IpAddress}|{log.UserAgent}|{log.Status}|" +
                   $"{log.ErrorMessage}|{log.CreatedAt:O}|{log.CorrelationId}|{log.PreviousHash}";

        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(data));
        return Convert.ToBase64String(hashBytes);
    }

    private async Task<string?> GetPreviousHashAsync()
    {
        var previousLog = await _context.AuditLogs
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync();

        return previousLog?.CurrentHash;
    }

    private async Task ExportToCsvAsync(List<AuditLogDto> logs, string filePath)
    {
        using var writer = new StreamWriter(filePath);

        // Write header
        await writer.WriteLineAsync("Id,UserId,UserName,Action,ResourceType,ResourceId,Status,ErrorMessage,IpAddress,UserAgent,CreatedAt,CorrelationId");

        // Write rows
        foreach (var log in logs)
        {
            var line = $"{log.Id},{log.UserId},{EscapeCsv(log.UserName)},{EscapeCsv(log.Action)}," +
                       $"{EscapeCsv(log.ResourceType)},{EscapeCsv(log.ResourceId)},{EscapeCsv(log.Status)}," +
                       $"{EscapeCsv(log.ErrorMessage)},{EscapeCsv(log.IpAddress)},{EscapeCsv(log.UserAgent)}," +
                       $"{log.CreatedAt:O},{log.CorrelationId}";

            await writer.WriteLineAsync(line);
        }
    }

    private async Task ExportToJsonAsync(List<AuditLogDto> logs, string filePath)
    {
        var json = JsonSerializer.Serialize(logs, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        await File.WriteAllTextAsync(filePath, json);
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
}
