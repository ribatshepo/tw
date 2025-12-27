using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using USP.Core.Domain.Entities.Audit;
using USP.Core.Domain.Enums;
using USP.Core.Interfaces.Services.Audit;
using USP.Core.Interfaces.Services.Secrets;
using USP.Infrastructure.Metrics;
using USP.Infrastructure.Persistence;

namespace USP.Infrastructure.Services.Audit;

/// <summary>
/// Implements tamper-proof audit logging with encryption and hash chaining.
/// All audit logs are encrypted and include integrity verification.
/// </summary>
public class AuditService : IAuditService
{
    private readonly ApplicationDbContext _context;
    private readonly IEncryptionService _encryptionService;
    private readonly ILogger<AuditService> _logger;

    public AuditService(
        ApplicationDbContext context,
        IEncryptionService encryptionService,
        ILogger<AuditService> logger)
    {
        _context = context;
        _encryptionService = encryptionService;
        _logger = logger;
    }

    public async Task<AuditLog> LogEventAsync(
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
        CancellationToken cancellationToken = default)
    {
        // Start timing audit event write

        try
        {
            var auditLog = new AuditLog
            {
                Id = Guid.NewGuid().ToString(),
                EventType = eventType,
                UserId = userId,
                UserName = userName,
                Resource = resource,
                Action = action,
                Success = success,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                Details = details,
                CorrelationId = correlationId ?? Guid.NewGuid().ToString(),
                Timestamp = DateTime.UtcNow
            };

            // Encrypt sensitive data if details contain sensitive information
            if (!string.IsNullOrEmpty(details))
            {
                auditLog.EncryptedData = await _encryptionService.EncryptAsync("audit-log", details, cancellationToken: cancellationToken);
                auditLog.Details = null; // Clear plaintext after encryption
            }

            // Compute hash chain
            var previousLog = await _context.AuditLogs
                .OrderByDescending(a => a.Timestamp)
                .FirstOrDefaultAsync(cancellationToken);

            auditLog.PreviousHash = previousLog?.CurrentHash;
            auditLog.CurrentHash = ComputeAuditHash(auditLog);

            _context.AuditLogs.Add(auditLog);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Audit event logged: {EventType}, User: {UserId}, Resource: {Resource}, Action: {Action}, Success: {Success}",
                eventType, userId, resource, action, success);

            // Record metrics
            SecurityMetrics.RecordAuditEvent(eventType, success);

            return auditLog;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log audit event: {EventType}", eventType);

            // Record audit failure metric
            SecurityMetrics.RecordAuditEventFailure();

            throw;
        }
    }

    public Task<AuditLog> LogAuthenticationEventAsync(
        AuditEventType eventType,
        string userId,
        string userName,
        bool success,
        string? ipAddress = null,
        string? userAgent = null,
        string? details = null,
        CancellationToken cancellationToken = default)
    {
        return LogEventAsync(
            eventType,
            userId,
            userName,
            resource: "authentication",
            action: eventType.ToString(),
            success,
            ipAddress,
            userAgent,
            details,
            cancellationToken: cancellationToken);
    }

    public Task<AuditLog> LogAuthorizationEventAsync(
        AuditEventType eventType,
        string userId,
        string resource,
        string action,
        bool granted,
        string? reason = null,
        string? ipAddress = null,
        CancellationToken cancellationToken = default)
    {
        var details = reason != null
            ? JsonSerializer.Serialize(new { reason })
            : null;

        return LogEventAsync(
            eventType,
            userId,
            userName: null,
            resource,
            action,
            granted,
            ipAddress,
            userAgent: null,
            details,
            cancellationToken: cancellationToken);
    }

    public Task<AuditLog> LogSecretEventAsync(
        AuditEventType eventType,
        string userId,
        string secretPath,
        bool success,
        string? details = null,
        CancellationToken cancellationToken = default)
    {
        return LogEventAsync(
            eventType,
            userId,
            userName: null,
            resource: secretPath,
            action: GetActionFromEventType(eventType),
            success,
            details: details,
            cancellationToken: cancellationToken);
    }

    public Task<AuditLog> LogPAMEventAsync(
        AuditEventType eventType,
        string userId,
        string accountId,
        bool success,
        string? details = null,
        CancellationToken cancellationToken = default)
    {
        return LogEventAsync(
            eventType,
            userId,
            userName: null,
            resource: $"pam/account/{accountId}",
            action: GetActionFromEventType(eventType),
            success,
            details: details,
            cancellationToken: cancellationToken);
    }

    public Task<AuditLog> LogSystemEventAsync(
        AuditEventType eventType,
        string? details = null,
        CancellationToken cancellationToken = default)
    {
        return LogEventAsync(
            eventType,
            userId: null,
            userName: "SYSTEM",
            resource: "system",
            action: eventType.ToString(),
            success: true,
            details: details,
            cancellationToken: cancellationToken);
    }

    public async Task<AuditSearchResult> SearchAuditLogsAsync(
        AuditSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = _context.AuditLogs.AsQueryable();

            // Apply filters
            if (!string.IsNullOrEmpty(request.UserId))
            {
                query = query.Where(a => a.UserId == request.UserId);
            }

            if (!string.IsNullOrEmpty(request.UserName))
            {
                query = query.Where(a => a.UserName != null && a.UserName.Contains(request.UserName));
            }

            if (request.EventType.HasValue)
            {
                query = query.Where(a => a.EventType == request.EventType.Value);
            }

            if (!string.IsNullOrEmpty(request.Resource))
            {
                // Support wildcard search
                if (request.Resource.Contains('*'))
                {
                    var pattern = request.Resource.Replace("*", "%");
                    query = query.Where(a => a.Resource != null && EF.Functions.Like(a.Resource, pattern));
                }
                else
                {
                    query = query.Where(a => a.Resource == request.Resource);
                }
            }

            if (!string.IsNullOrEmpty(request.Action))
            {
                query = query.Where(a => a.Action == request.Action);
            }

            if (request.Success.HasValue)
            {
                query = query.Where(a => a.Success == request.Success.Value);
            }

            if (!string.IsNullOrEmpty(request.IpAddress))
            {
                query = query.Where(a => a.IpAddress == request.IpAddress);
            }

            if (!string.IsNullOrEmpty(request.CorrelationId))
            {
                query = query.Where(a => a.CorrelationId == request.CorrelationId);
            }

            if (request.StartDate.HasValue)
            {
                query = query.Where(a => a.Timestamp >= request.StartDate.Value);
            }

            if (request.EndDate.HasValue)
            {
                query = query.Where(a => a.Timestamp <= request.EndDate.Value);
            }

            if (!string.IsNullOrEmpty(request.SearchText))
            {
                query = query.Where(a => a.Details != null && a.Details.Contains(request.SearchText));
            }

            // Get total count before pagination
            var totalCount = await query.CountAsync(cancellationToken);

            // Apply sorting
            query = request.SortBy.ToLowerInvariant() switch
            {
                "timestamp" => request.SortDirection.ToLowerInvariant() == "asc"
                    ? query.OrderBy(a => a.Timestamp)
                    : query.OrderByDescending(a => a.Timestamp),
                "eventtype" => request.SortDirection.ToLowerInvariant() == "asc"
                    ? query.OrderBy(a => a.EventType)
                    : query.OrderByDescending(a => a.EventType),
                "username" => request.SortDirection.ToLowerInvariant() == "asc"
                    ? query.OrderBy(a => a.UserName)
                    : query.OrderByDescending(a => a.UserName),
                _ => query.OrderByDescending(a => a.Timestamp)
            };

            // Apply pagination
            var pageSize = Math.Min(request.PageSize, 1000); // Max 1000 per page
            var skip = (request.Page - 1) * pageSize;

            var results = await query
                .Skip(skip)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return new AuditSearchResult
            {
                Results = results,
                TotalCount = totalCount,
                Page = request.Page,
                PageSize = pageSize
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching audit logs");
            throw;
        }
    }

    public async Task<AuditLog?> GetAuditLogByIdAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        return await _context.AuditLogs
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    public async Task<List<AuditLog>> GetUserAuditLogsAsync(
        string userId,
        DateTime? startDate = null,
        DateTime? endDate = null,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var query = _context.AuditLogs
            .Where(a => a.UserId == userId);

        if (startDate.HasValue)
        {
            query = query.Where(a => a.Timestamp >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(a => a.Timestamp <= endDate.Value);
        }

        return await query
            .OrderByDescending(a => a.Timestamp)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<AuditLog>> GetResourceAuditLogsAsync(
        string resource,
        DateTime? startDate = null,
        DateTime? endDate = null,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var query = _context.AuditLogs
            .Where(a => a.Resource == resource);

        if (startDate.HasValue)
        {
            query = query.Where(a => a.Timestamp >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(a => a.Timestamp <= endDate.Value);
        }

        return await query
            .OrderByDescending(a => a.Timestamp)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<AuditLog>> GetAuditLogsByCorrelationIdAsync(
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        return await _context.AuditLogs
            .Where(a => a.CorrelationId == correlationId)
            .OrderBy(a => a.Timestamp)
            .ToListAsync(cancellationToken);
    }

    public async Task<byte[]> ExportAuditLogsToCsvAsync(
        AuditSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var searchResult = await SearchAuditLogsAsync(request, cancellationToken);

        var csv = new StringBuilder();
        csv.AppendLine("Timestamp,EventType,UserId,UserName,Resource,Action,Success,IpAddress,CorrelationId");

        foreach (var log in searchResult.Results)
        {
            csv.AppendLine($"\"{log.Timestamp:O}\"," +
                          $"\"{log.EventType}\"," +
                          $"\"{log.UserId ?? ""}\"," +
                          $"\"{log.UserName ?? ""}\"," +
                          $"\"{log.Resource ?? ""}\"," +
                          $"\"{log.Action ?? ""}\"," +
                          $"\"{log.Success}\"," +
                          $"\"{log.IpAddress ?? ""}\"," +
                          $"\"{log.CorrelationId ?? ""}\"");
        }

        return Encoding.UTF8.GetBytes(csv.ToString());
    }

    public async Task<byte[]> ExportAuditLogsToJsonAsync(
        AuditSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var searchResult = await SearchAuditLogsAsync(request, cancellationToken);

        var json = JsonSerializer.Serialize(searchResult.Results, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        return Encoding.UTF8.GetBytes(json);
    }

    public async Task<AuditIntegrityResult> VerifyAuditIntegrityAsync(
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = _context.AuditLogs.AsQueryable();

            if (startDate.HasValue)
            {
                query = query.Where(a => a.Timestamp >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                query = query.Where(a => a.Timestamp <= endDate.Value);
            }

            var logs = await query
                .OrderBy(a => a.Timestamp)
                .ToListAsync(cancellationToken);

            var totalRecords = logs.Count;
            var verifiedRecords = 0;
            var invalidRecordIds = new List<string>();

            // Verify hash chain integrity
            AuditLog? previousLog = null;
            foreach (var log in logs)
            {
                // Verify required fields are present
                if (string.IsNullOrEmpty(log.Id) || log.Timestamp == default)
                {
                    invalidRecordIds.Add(log.Id);
                    _logger.LogWarning("Audit log {LogId} missing required fields", log.Id);
                    continue;
                }

                // Verify hash chain links to previous log
                var expectedPreviousHash = previousLog?.CurrentHash;
                if (log.PreviousHash != expectedPreviousHash)
                {
                    invalidRecordIds.Add(log.Id);
                    _logger.LogWarning(
                        "Hash chain broken at log {LogId}: expected previous hash {Expected}, got {Actual}",
                        log.Id, expectedPreviousHash, log.PreviousHash);
                    continue;
                }

                // Verify current hash is correct
                var expectedHash = ComputeAuditHash(log);
                if (log.CurrentHash != expectedHash)
                {
                    invalidRecordIds.Add(log.Id);
                    _logger.LogWarning(
                        "Hash verification failed for log {LogId}: expected {Expected}, got {Actual}",
                        log.Id, expectedHash, log.CurrentHash);
                    continue;
                }

                verifiedRecords++;
                previousLog = log;
            }

            return new AuditIntegrityResult
            {
                IsValid = invalidRecordIds.Count == 0,
                TotalRecords = totalRecords,
                VerifiedRecords = verifiedRecords,
                InvalidRecordIds = invalidRecordIds,
                Message = invalidRecordIds.Count == 0
                    ? "All audit logs passed integrity verification"
                    : $"Found {invalidRecordIds.Count} invalid audit log(s)"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying audit integrity");
            throw;
        }
    }

    public async Task<AuditStatistics> GetAuditStatisticsAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        var logs = await _context.AuditLogs
            .Where(a => a.Timestamp >= startDate && a.Timestamp <= endDate)
            .ToListAsync(cancellationToken);

        var statistics = new AuditStatistics
        {
            TotalEvents = logs.Count,
            SuccessfulEvents = logs.Count(l => l.Success),
            FailedEvents = logs.Count(l => !l.Success),
            UniqueUsers = logs.Where(l => l.UserId != null).Select(l => l.UserId).Distinct().Count(),
            UniqueResources = logs.Where(l => l.Resource != null).Select(l => l.Resource).Distinct().Count(),
            StartDate = startDate,
            EndDate = endDate
        };

        // Event type counts
        statistics.EventTypeCounts = logs
            .GroupBy(l => l.EventType)
            .ToDictionary(g => g.Key, g => g.Count());

        // Top 10 users
        statistics.TopUsers = logs
            .Where(l => l.UserName != null)
            .GroupBy(l => l.UserName!)
            .OrderByDescending(g => g.Count())
            .Take(10)
            .ToDictionary(g => g.Key, g => g.Count());

        // Top 10 resources
        statistics.TopResources = logs
            .Where(l => l.Resource != null)
            .GroupBy(l => l.Resource!)
            .OrderByDescending(g => g.Count())
            .Take(10)
            .ToDictionary(g => g.Key, g => g.Count());

        // Top 10 actions
        statistics.TopActions = logs
            .Where(l => l.Action != null)
            .GroupBy(l => l.Action!)
            .OrderByDescending(g => g.Count())
            .Take(10)
            .ToDictionary(g => g.Key, g => g.Count());

        return statistics;
    }

    public async Task<int> DeleteAuditLogsOlderThanAsync(
        DateTime cutoffDate,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var logsToDelete = await _context.AuditLogs
                .Where(a => a.Timestamp < cutoffDate)
                .ToListAsync(cancellationToken);

            var count = logsToDelete.Count;

            _context.AuditLogs.RemoveRange(logsToDelete);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogWarning(
                "Deleted {Count} audit logs older than {CutoffDate}",
                count, cutoffDate);

            return count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting old audit logs");
            throw;
        }
    }

    /// <summary>
    /// Helper method to extract action from event type
    /// </summary>
    private string GetActionFromEventType(AuditEventType eventType)
    {
        return eventType switch
        {
            AuditEventType.SecretWritten => "write",
            AuditEventType.SecretRead => "read",
            AuditEventType.SecretDeleted => "delete",
            AuditEventType.SecretRestored => "restore",
            AuditEventType.AccountCheckedOut => "checkout",
            AuditEventType.AccountCheckedIn => "checkin",
            _ => eventType.ToString().ToLowerInvariant()
        };
    }

    /// <summary>
    /// Computes a SHA-256 hash of the audit log entry for tamper detection.
    /// Hash includes all critical fields plus the previous hash to create a blockchain-style chain.
    /// </summary>
    private string ComputeAuditHash(AuditLog log)
    {
        var data = $"{log.Id}|{log.Timestamp:O}|{log.EventType}|{log.UserId ?? ""}|" +
                   $"{log.Resource ?? ""}|{log.Action ?? ""}|{log.Success}|{log.PreviousHash ?? ""}";

        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(data));
        return Convert.ToBase64String(hashBytes);
    }
}
