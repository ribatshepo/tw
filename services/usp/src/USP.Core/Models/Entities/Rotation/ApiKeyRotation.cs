namespace USP.Core.Models.Entities.Rotation;

/// <summary>
/// Represents API key rotation configuration
/// </summary>
public class ApiKeyRotation
{
    public Guid Id { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public string ApiKeyName { get; set; } = string.Empty;
    public string KeyType { get; set; } = "service_account"; // service_account, user, application
    public Guid? AssociatedServiceId { get; set; }
    public Guid? AssociatedUserId { get; set; }
    public int RotationIntervalDays { get; set; } = 90;
    public string RotationPolicy { get; set; } = "blue_green"; // blue_green, immediate, manual
    public string? CronExpression { get; set; }
    public bool AutoRotate { get; set; } = true;
    public int OverlapPeriodHours { get; set; } = 24; // For blue-green rotation
    public string? CurrentActiveKey { get; set; } // Encrypted
    public string? CurrentStandbyKey { get; set; } // Encrypted
    public DateTime? CurrentKeyCreatedAt { get; set; }
    public DateTime? NextRotationDate { get; set; }
    public DateTime? LastRotationDate { get; set; }
    public string Status { get; set; } = "active"; // active, rotating, failed, disabled
    public string? LastRotationStatus { get; set; }
    public string? LastRotationError { get; set; }
    public bool TrackUsage { get; set; } = true;
    public long TotalRequests { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public string? NotificationEmail { get; set; }
    public string? NotificationWebhook { get; set; }
    public Guid? OwnerId { get; set; }
    public string? Tags { get; set; } // JSON
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
