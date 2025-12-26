namespace USP.Core.Models.DTOs.Rotation;

public class ApiKeyRotationDto
{
    public Guid Id { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public string ApiKeyName { get; set; } = string.Empty;
    public string KeyType { get; set; } = string.Empty;
    public int RotationIntervalDays { get; set; }
    public string RotationPolicy { get; set; } = string.Empty;
    public bool AutoRotate { get; set; }
    public int OverlapPeriodHours { get; set; }
    public DateTime? NextRotationDate { get; set; }
    public DateTime? LastRotationDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public long TotalRequests { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateApiKeyRotationRequest
{
    public string ServiceName { get; set; } = string.Empty;
    public string ApiKeyName { get; set; } = string.Empty;
    public string KeyType { get; set; } = "service_account";
    public int RotationIntervalDays { get; set; } = 90;
    public string RotationPolicy { get; set; } = "blue_green";
    public string? CronExpression { get; set; }
    public bool AutoRotate { get; set; } = true;
    public int OverlapPeriodHours { get; set; } = 24;
    public bool TrackUsage { get; set; } = true;
    public string? NotificationEmail { get; set; }
    public Dictionary<string, string>? Tags { get; set; }
}

public class ApiKeyRotationResultDto
{
    public Guid Id { get; set; }
    public bool Success { get; set; }
    public string? NewActiveKey { get; set; }
    public string? StandbyKey { get; set; }
    public DateTime? NewKeyExpiresAt { get; set; }
    public string? ErrorMessage { get; set; }
    public TimeSpan RotationDuration { get; set; }
}

public class ApiKeyUsageDto
{
    public Guid ApiKeyRotationId { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public long TotalRequests { get; set; }
    public long RequestsLast24Hours { get; set; }
    public long RequestsLast7Days { get; set; }
    public long RequestsLast30Days { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public List<DailyUsageDto> DailyUsage { get; set; } = new();
}

public class DailyUsageDto
{
    public DateTime Date { get; set; }
    public long RequestCount { get; set; }
}
