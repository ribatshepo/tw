using System.ComponentModel.DataAnnotations;
using USP.Core.Domain.Enums;

namespace USP.Core.Domain.Entities.Integration;

/// <summary>
/// Represents a webhook delivery attempt
/// </summary>
public class WebhookDelivery
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    public string WebhookId { get; set; } = null!;

    [Required]
    public WebhookEventType EventType { get; set; }

    [Required]
    public WebhookDeliveryStatus Status { get; set; }

    public string? Payload { get; set; }  // JSON

    public int AttemptCount { get; set; }

    public int? ResponseStatusCode { get; set; }

    public string? ResponseBody { get; set; }

    public string? ErrorMessage { get; set; }

    public DateTime? NextRetryAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? DeliveredAt { get; set; }

    public virtual Webhook Webhook { get; set; } = null!;
}
