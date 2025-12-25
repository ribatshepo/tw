using USP.Core.Models.DTOs.Webhook;

namespace USP.Core.Services.Webhook;

/// <summary>
/// Service for webhook management and event publishing
/// </summary>
public interface IWebhookService
{
    /// <summary>
    /// Create a new webhook
    /// </summary>
    Task<WebhookDto> CreateWebhookAsync(Guid userId, CreateWebhookRequest request);

    /// <summary>
    /// Get webhook by ID
    /// </summary>
    Task<WebhookDto?> GetWebhookByIdAsync(Guid id, Guid userId);

    /// <summary>
    /// Get all webhooks for a user
    /// </summary>
    Task<List<WebhookDto>> GetWebhooksAsync(Guid userId, bool? activeOnly = null);

    /// <summary>
    /// Update webhook
    /// </summary>
    Task<bool> UpdateWebhookAsync(Guid id, Guid userId, UpdateWebhookRequest request);

    /// <summary>
    /// Delete webhook
    /// </summary>
    Task<bool> DeleteWebhookAsync(Guid id, Guid userId);

    /// <summary>
    /// Test webhook with custom payload
    /// </summary>
    Task<WebhookDeliveryDto> TestWebhookAsync(Guid id, Guid userId, TestWebhookRequest request);

    /// <summary>
    /// Get webhook deliveries
    /// </summary>
    Task<(List<WebhookDeliveryDto> Deliveries, int TotalCount)> GetDeliveriesAsync(WebhookDeliveryFilterRequest request, Guid userId);

    /// <summary>
    /// Redeliver a failed webhook delivery
    /// </summary>
    Task<WebhookDeliveryDto?> RedeliverAsync(Guid webhookId, Guid deliveryId, Guid userId);

    /// <summary>
    /// Publish event to matching webhooks (trigger webhook delivery)
    /// </summary>
    Task PublishEventAsync(string eventType, object eventData, Guid? userId = null, string? correlationId = null);

    /// <summary>
    /// Get available webhook events
    /// </summary>
    Task<List<string>> GetAvailableEventsAsync();

    /// <summary>
    /// Reset circuit breaker for a webhook
    /// </summary>
    Task<bool> ResetCircuitBreakerAsync(Guid id, Guid userId);
}
