namespace USP.Core.Models.DTOs.Audit;

/// <summary>
/// Request for searching audit logs
/// </summary>
public class AuditSearchRequest
{
    public Guid? UserId { get; set; }
    public string? Action { get; set; }
    public string? ResourceType { get; set; }
    public string? ResourceId { get; set; }
    public string? Status { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? IpAddress { get; set; }
    public string? SearchTerm { get; set; } // Full-text search
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

/// <summary>
/// Audit log DTO
/// </summary>
public class AuditLogDto
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public string? UserName { get; set; }
    public string Action { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public string? ResourceId { get; set; }
    public object? OldValue { get; set; }
    public object? NewValue { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CorrelationId { get; set; }
}

/// <summary>
/// Audit log export request
/// </summary>
public class AuditExportRequest
{
    public AuditSearchRequest SearchCriteria { get; set; } = new();
    public string Format { get; set; } = "CSV"; // CSV, JSON, PDF
    public bool IncludeSensitiveData { get; set; } = false;
}

/// <summary>
/// Audit statistics
/// </summary>
public class AuditStatisticsDto
{
    public int TotalLogs { get; set; }
    public int TodayLogs { get; set; }
    public int FailedActions { get; set; }
    public Dictionary<string, int> ActionBreakdown { get; set; } = new();
    public Dictionary<string, int> UserBreakdown { get; set; } = new();
    public List<TopResource> TopResources { get; set; } = new();
}

public class TopResource
{
    public string ResourceType { get; set; } = string.Empty;
    public int Count { get; set; }
}
