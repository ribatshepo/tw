using System.ComponentModel.DataAnnotations;
using USP.Core.Domain.Enums;

namespace USP.Core.Domain.Entities.Audit;

/// <summary>
/// Represents an encrypted, tamper-proof audit log entry
/// </summary>
public class AuditLog
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    public AuditEventType EventType { get; set; }

    public string? UserId { get; set; }

    [MaxLength(255)]
    public string? UserName { get; set; }

    [MaxLength(45)]
    public string? IpAddress { get; set; }

    [MaxLength(500)]
    public string? UserAgent { get; set; }

    [MaxLength(255)]
    public string? Resource { get; set; }

    [MaxLength(100)]
    public string? Action { get; set; }

    public bool Success { get; set; }

    public string? Details { get; set; }  // JSON

    public string? EncryptedData { get; set; }

    [MaxLength(255)]
    public string? CorrelationId { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
