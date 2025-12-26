namespace USP.Core.Domain.Enums;

/// <summary>
/// Represents the delivery status of a webhook
/// </summary>
public enum WebhookDeliveryStatus
{
    /// <summary>
    /// Webhook delivery is pending (not yet attempted)
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Webhook delivery succeeded
    /// </summary>
    Success = 1,

    /// <summary>
    /// Webhook delivery failed
    /// </summary>
    Failed = 2,

    /// <summary>
    /// Webhook delivery is being retried
    /// </summary>
    Retrying = 3,

    /// <summary>
    /// Webhook delivery failed after all retries
    /// </summary>
    Exhausted = 4
}
