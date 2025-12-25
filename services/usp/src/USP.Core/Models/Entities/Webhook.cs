namespace USP.Core.Models.Entities;

/// <summary>
/// Webhook registration entity
/// </summary>
public class Webhook
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<string> Events { get; set; } = new(); // user.created, secret.written, etc.
    public bool Active { get; set; } = true;
    public string AuthenticationType { get; set; } = "secret"; // secret, oauth2, mtls, none
    public string? SecretToken { get; set; } // HMAC secret
    public string? OAuth2ClientId { get; set; }
    public string? OAuth2ClientSecret { get; set; }
    public string? OAuth2TokenUrl { get; set; }
    public string? CustomHeaders { get; set; } // JSON object
    public string? PayloadTemplate { get; set; } // Custom payload transformation template
    public int MaxRetries { get; set; } = 5;
    public int TimeoutSeconds { get; set; } = 30;
    public bool VerifySsl { get; set; } = true;

    // Circuit breaker state
    public string CircuitBreakerState { get; set; } = "closed"; // closed, open, half-open
    public int ConsecutiveFailures { get; set; } = 0;
    public DateTime? CircuitBreakerOpenedAt { get; set; }
    public int CircuitBreakerThreshold { get; set; } = 5; // Open after N failures
    public int CircuitBreakerResetMinutes { get; set; } = 5; // Try again after N minutes

    // Statistics
    public int TotalDeliveries { get; set; } = 0;
    public int SuccessfulDeliveries { get; set; } = 0;
    public int FailedDeliveries { get; set; } = 0;
    public DateTime? LastTriggeredAt { get; set; }
    public DateTime? LastSuccessAt { get; set; }
    public DateTime? LastFailureAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public virtual ApplicationUser User { get; set; } = null!;
    public virtual ICollection<WebhookDelivery> Deliveries { get; set; } = new List<WebhookDelivery>();
}

/// <summary>
/// Webhook delivery log entity
/// </summary>
public class WebhookDelivery
{
    public Guid Id { get; set; }
    public Guid WebhookId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty; // JSON payload
    public string Status { get; set; } = "pending"; // pending, delivered, failed
    public int AttemptCount { get; set; } = 0;
    public int ResponseStatus { get; set; } = 0;
    public string? ResponseBody { get; set; }
    public string? ErrorMessage { get; set; }
    public int DurationMs { get; set; } = 0;
    public string? HmacSignature { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeliveredAt { get; set; }
    public DateTime? NextRetryAt { get; set; }

    // Navigation properties
    public virtual Webhook Webhook { get; set; } = null!;
}
