using System.ComponentModel.DataAnnotations;
using USP.Core.Domain.Enums;

namespace USP.Core.Domain.Entities.Integration;

/// <summary>
/// Represents a webhook subscription
/// </summary>
public class Webhook
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    public string UserId { get; set; } = null!;

    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = null!;

    [Required]
    [MaxLength(500)]
    public string Url { get; set; } = null!;

    [Required]
    public string Events { get; set; } = "[]";  // JSON array of WebhookEventType

    [MaxLength(255)]
    public string? SecretKey { get; set; }  // For HMAC signature

    public bool IsActive { get; set; } = true;

    public int MaxRetries { get; set; } = 3;

    public int TimeoutSeconds { get; set; } = 30;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? DeletedAt { get; set; }

    public virtual ICollection<WebhookDelivery> Deliveries { get; set; } = new List<WebhookDelivery>();
}
