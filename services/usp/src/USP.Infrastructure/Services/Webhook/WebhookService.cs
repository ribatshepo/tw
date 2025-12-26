using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using USP.Core.Models.DTOs.Webhook;
using USP.Core.Models.Entities;
using USP.Core.Services.Webhook;
using USP.Infrastructure.Data;

namespace USP.Infrastructure.Services.Webhook;

public class WebhookService : IWebhookService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<WebhookService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    private const string HmacHeader = "X-Webhook-Signature";
    private const string EventTypeHeader = "X-Event-Type";
    private const string DeliveryIdHeader = "X-Delivery-ID";
    private const string CorrelationIdHeader = "X-Correlation-ID";

    public WebhookService(
        ApplicationDbContext context,
        ILogger<WebhookService> logger,
        IHttpClientFactory httpClientFactory)
    {
        _context = context;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<WebhookDto> CreateWebhookAsync(Guid userId, CreateWebhookRequest request)
    {
        // Validate URL
        if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var uri))
            throw new ArgumentException("Invalid URL format", nameof(request.Url));

        // Validate events
        var validEvents = WebhookEvents.GetAll();
        var invalidEvents = request.Events.Where(e => !validEvents.Contains(e)).ToList();
        if (invalidEvents.Count > 0)
            throw new ArgumentException($"Invalid events: {string.Join(", ", invalidEvents)}", nameof(request.Events));

        // Create webhook
        var webhook = new Core.Models.Entities.Webhook
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = request.Name,
            Url = request.Url,
            Description = request.Description,
            Events = request.Events,
            Active = true,
            AuthenticationType = request.AuthenticationType.ToLower(),
            SecretToken = request.SecretToken,
            OAuth2ClientId = request.OAuth2ClientId,
            OAuth2ClientSecret = request.OAuth2ClientSecret,
            OAuth2TokenUrl = request.OAuth2TokenUrl,
            CustomHeaders = request.CustomHeaders != null ? JsonSerializer.Serialize(request.CustomHeaders) : null,
            PayloadTemplate = request.PayloadTemplate,
            MaxRetries = request.MaxRetries,
            TimeoutSeconds = request.TimeoutSeconds,
            VerifySsl = request.VerifySsl,
            CircuitBreakerThreshold = request.CircuitBreakerThreshold,
            CircuitBreakerResetMinutes = request.CircuitBreakerResetMinutes,
            CircuitBreakerState = "closed",
            CreatedAt = DateTime.UtcNow
        };

        _context.Webhooks.Add(webhook);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Webhook {WebhookId} created for user {UserId}", webhook.Id, userId);

        return MapToDto(webhook);
    }

    public async Task<WebhookDto?> GetWebhookByIdAsync(Guid id, Guid userId)
    {
        var webhook = await _context.Webhooks
            .FirstOrDefaultAsync(w => w.Id == id && w.UserId == userId);

        return webhook != null ? MapToDto(webhook) : null;
    }

    public async Task<List<WebhookDto>> GetWebhooksAsync(Guid userId, bool? activeOnly = null)
    {
        var query = _context.Webhooks.Where(w => w.UserId == userId);

        if (activeOnly.HasValue)
            query = query.Where(w => w.Active == activeOnly.Value);

        var webhooks = await query.OrderByDescending(w => w.CreatedAt).ToListAsync();

        return webhooks.Select(MapToDto).ToList();
    }

    public async Task<bool> UpdateWebhookAsync(Guid id, Guid userId, UpdateWebhookRequest request)
    {
        var webhook = await _context.Webhooks
            .FirstOrDefaultAsync(w => w.Id == id && w.UserId == userId);

        if (webhook == null)
            return false;

        // Update fields
        if (!string.IsNullOrWhiteSpace(request.Name))
            webhook.Name = request.Name;

        if (!string.IsNullOrWhiteSpace(request.Url))
        {
            if (!Uri.TryCreate(request.Url, UriKind.Absolute, out _))
                throw new ArgumentException("Invalid URL format", nameof(request.Url));
            webhook.Url = request.Url;
        }

        if (request.Description != null)
            webhook.Description = request.Description;

        if (request.Events != null)
        {
            var validEvents = WebhookEvents.GetAll();
            var invalidEvents = request.Events.Where(e => !validEvents.Contains(e)).ToList();
            if (invalidEvents.Count > 0)
                throw new ArgumentException($"Invalid events: {string.Join(", ", invalidEvents)}", nameof(request.Events));
            webhook.Events = request.Events;
        }

        if (request.Active.HasValue)
            webhook.Active = request.Active.Value;

        if (!string.IsNullOrWhiteSpace(request.AuthenticationType))
            webhook.AuthenticationType = request.AuthenticationType.ToLower();

        if (request.SecretToken != null)
            webhook.SecretToken = request.SecretToken;

        if (request.CustomHeaders != null)
            webhook.CustomHeaders = JsonSerializer.Serialize(request.CustomHeaders);

        if (request.PayloadTemplate != null)
            webhook.PayloadTemplate = request.PayloadTemplate;

        if (request.MaxRetries.HasValue)
            webhook.MaxRetries = request.MaxRetries.Value;

        if (request.TimeoutSeconds.HasValue)
            webhook.TimeoutSeconds = request.TimeoutSeconds.Value;

        if (request.VerifySsl.HasValue)
            webhook.VerifySsl = request.VerifySsl.Value;

        webhook.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Webhook {WebhookId} updated", id);

        return true;
    }

    public async Task<bool> DeleteWebhookAsync(Guid id, Guid userId)
    {
        var webhook = await _context.Webhooks
            .FirstOrDefaultAsync(w => w.Id == id && w.UserId == userId);

        if (webhook == null)
            return false;

        _context.Webhooks.Remove(webhook);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Webhook {WebhookId} deleted", id);

        return true;
    }

    public async Task<WebhookDeliveryDto> TestWebhookAsync(Guid id, Guid userId, TestWebhookRequest request)
    {
        var webhook = await _context.Webhooks
            .FirstOrDefaultAsync(w => w.Id == id && w.UserId == userId);

        if (webhook == null)
            throw new InvalidOperationException("Webhook not found");

        var testPayload = request.TestPayload ?? new Dictionary<string, object>
        {
            { "test", true },
            { "message", "This is a test webhook delivery" },
            { "timestamp", DateTime.UtcNow }
        };

        var delivery = await CreateAndDeliverWebhookAsync(webhook, request.EventType ?? "webhook.test", testPayload, null);

        return MapDeliveryToDto(delivery, webhook.Name);
    }

    public async Task<(List<WebhookDeliveryDto> Deliveries, int TotalCount)> GetDeliveriesAsync(
        WebhookDeliveryFilterRequest request, Guid userId)
    {
        // Verify user owns the webhook if webhook ID is specified
        if (request.WebhookId.HasValue)
        {
            var webhook = await _context.Webhooks
                .FirstOrDefaultAsync(w => w.Id == request.WebhookId.Value && w.UserId == userId);

            if (webhook == null)
                return (new List<WebhookDeliveryDto>(), 0);
        }

        var query = _context.WebhookDeliveries
            .Include(d => d.Webhook)
            .Where(d => d.Webhook.UserId == userId);

        if (request.WebhookId.HasValue)
            query = query.Where(d => d.WebhookId == request.WebhookId.Value);

        if (!string.IsNullOrWhiteSpace(request.EventType))
            query = query.Where(d => d.EventType == request.EventType);

        if (!string.IsNullOrWhiteSpace(request.Status))
            query = query.Where(d => d.Status == request.Status);

        if (request.StartDate.HasValue)
            query = query.Where(d => d.CreatedAt >= request.StartDate.Value);

        if (request.EndDate.HasValue)
            query = query.Where(d => d.CreatedAt <= request.EndDate.Value);

        var totalCount = await query.CountAsync();

        var deliveries = await query
            .OrderByDescending(d => d.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        var dtos = deliveries.Select(d => MapDeliveryToDto(d, d.Webhook.Name)).ToList();

        return (dtos, totalCount);
    }

    public async Task<WebhookDeliveryDto?> RedeliverAsync(Guid webhookId, Guid deliveryId, Guid userId)
    {
        var webhook = await _context.Webhooks
            .FirstOrDefaultAsync(w => w.Id == webhookId && w.UserId == userId);

        if (webhook == null)
            return null;

        var delivery = await _context.WebhookDeliveries
            .FirstOrDefaultAsync(d => d.Id == deliveryId && d.WebhookId == webhookId);

        if (delivery == null)
            return null;

        // Parse payload
        var payload = JsonSerializer.Deserialize<Dictionary<string, object>>(delivery.Payload);

        // Create new delivery
        var newDelivery = await CreateAndDeliverWebhookAsync(webhook, delivery.EventType, payload!, null);

        return MapDeliveryToDto(newDelivery, webhook.Name);
    }

    public async Task PublishEventAsync(string eventType, object eventData, Guid? userId = null, string? correlationId = null)
    {
        // Find matching webhooks
        var query = _context.Webhooks.Where(w => w.Active && w.Events.Contains(eventType));

        if (userId.HasValue)
            query = query.Where(w => w.UserId == userId.Value);

        var webhooks = await query.ToListAsync();

        if (webhooks.Count == 0)
        {
            _logger.LogDebug("No active webhooks found for event {EventType}", eventType);
            return;
        }

        _logger.LogInformation("Publishing event {EventType} to {Count} webhooks", eventType, webhooks.Count);

        // Trigger deliveries asynchronously
        var deliveryTasks = webhooks.Select(webhook =>
            CreateAndDeliverWebhookAsync(webhook, eventType, eventData, correlationId));

        await Task.WhenAll(deliveryTasks);
    }

    public async Task<List<string>> GetAvailableEventsAsync()
    {
        return await Task.FromResult(WebhookEvents.GetAll());
    }

    public async Task<bool> ResetCircuitBreakerAsync(Guid id, Guid userId)
    {
        var webhook = await _context.Webhooks
            .FirstOrDefaultAsync(w => w.Id == id && w.UserId == userId);

        if (webhook == null)
            return false;

        webhook.CircuitBreakerState = "closed";
        webhook.ConsecutiveFailures = 0;
        webhook.CircuitBreakerOpenedAt = null;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Circuit breaker reset for webhook {WebhookId}", id);

        return true;
    }

    // Private helper methods

    private async Task<WebhookDelivery> CreateAndDeliverWebhookAsync(
        Core.Models.Entities.Webhook webhook,
        string eventType,
        object eventData,
        string? correlationId)
    {
        // Check circuit breaker
        if (webhook.CircuitBreakerState == "open")
        {
            if (webhook.CircuitBreakerOpenedAt.HasValue &&
                DateTime.UtcNow < webhook.CircuitBreakerOpenedAt.Value.AddMinutes(webhook.CircuitBreakerResetMinutes))
            {
                _logger.LogWarning("Circuit breaker is open for webhook {WebhookId}, skipping delivery", webhook.Id);

                var skipDelivery = new WebhookDelivery
                {
                    Id = Guid.NewGuid(),
                    WebhookId = webhook.Id,
                    EventType = eventType,
                    Payload = JsonSerializer.Serialize(eventData),
                    Status = "failed",
                    ErrorMessage = "Circuit breaker is open",
                    CreatedAt = DateTime.UtcNow
                };

                _context.WebhookDeliveries.Add(skipDelivery);
                await _context.SaveChangesAsync();

                return skipDelivery;
            }
            else
            {
                // Try half-open state
                webhook.CircuitBreakerState = "half-open";
                await _context.SaveChangesAsync();
            }
        }

        // Create delivery record
        var delivery = new WebhookDelivery
        {
            Id = Guid.NewGuid(),
            WebhookId = webhook.Id,
            EventType = eventType,
            Payload = JsonSerializer.Serialize(eventData),
            Status = "pending",
            CreatedAt = DateTime.UtcNow
        };

        _context.WebhookDeliveries.Add(delivery);
        await _context.SaveChangesAsync();

        // Attempt delivery with retries
        await AttemptDeliveryWithRetriesAsync(webhook, delivery, correlationId);

        return delivery;
    }

    private async Task AttemptDeliveryWithRetriesAsync(
        Core.Models.Entities.Webhook webhook,
        WebhookDelivery delivery,
        string? correlationId)
    {
        var maxAttempts = webhook.MaxRetries + 1;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            delivery.AttemptCount = attempt;
            await _context.SaveChangesAsync();

            var success = await DeliverWebhookAsync(webhook, delivery, correlationId);

            if (success)
            {
                // Update webhook statistics
                webhook.TotalDeliveries++;
                webhook.SuccessfulDeliveries++;
                webhook.LastTriggeredAt = DateTime.UtcNow;
                webhook.LastSuccessAt = DateTime.UtcNow;
                webhook.ConsecutiveFailures = 0;

                // Close circuit breaker if in half-open state
                if (webhook.CircuitBreakerState == "half-open")
                    webhook.CircuitBreakerState = "closed";

                await _context.SaveChangesAsync();
                return;
            }

            // Calculate exponential backoff for retry
            if (attempt < maxAttempts)
            {
                var delaySeconds = Math.Pow(2, attempt - 1); // 1s, 2s, 4s, 8s, 16s...
                delivery.NextRetryAt = DateTime.UtcNow.AddSeconds(delaySeconds);
                await _context.SaveChangesAsync();

                _logger.LogWarning("Webhook delivery {DeliveryId} failed, retry {Attempt}/{MaxAttempts} in {Delay}s",
                    delivery.Id, attempt, maxAttempts, delaySeconds);

                await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
            }
        }

        // All retries exhausted - mark as failed
        delivery.Status = "failed";
        delivery.NextRetryAt = null;
        await _context.SaveChangesAsync();

        // Update webhook statistics and circuit breaker
        webhook.TotalDeliveries++;
        webhook.FailedDeliveries++;
        webhook.LastTriggeredAt = DateTime.UtcNow;
        webhook.LastFailureAt = DateTime.UtcNow;
        webhook.ConsecutiveFailures++;

        // Open circuit breaker if threshold reached
        if (webhook.ConsecutiveFailures >= webhook.CircuitBreakerThreshold)
        {
            webhook.CircuitBreakerState = "open";
            webhook.CircuitBreakerOpenedAt = DateTime.UtcNow;
            _logger.LogWarning("Circuit breaker opened for webhook {WebhookId} after {Failures} consecutive failures",
                webhook.Id, webhook.ConsecutiveFailures);
        }

        await _context.SaveChangesAsync();
    }

    private async Task<bool> DeliverWebhookAsync(
        Core.Models.Entities.Webhook webhook,
        WebhookDelivery delivery,
        string? correlationId)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(webhook.TimeoutSeconds);

            // Prepare payload
            var payload = delivery.Payload;

            // Apply custom payload template if configured
            if (!string.IsNullOrWhiteSpace(webhook.PayloadTemplate))
            {
                // Template substitution using string replacement
                // Future enhancement: Integrate Liquid, Handlebars, or Scriban template engine
                payload = webhook.PayloadTemplate
                    .Replace("{{payload}}", delivery.Payload)
                    .Replace("{{event_type}}", delivery.EventType)
                    .Replace("{{delivery_id}}", delivery.Id.ToString())
                    .Replace("{{timestamp}}", delivery.CreatedAt.ToString("O"));
            }

            // Calculate HMAC signature if secret token is configured
            string? hmacSignature = null;
            if (webhook.AuthenticationType == "secret" && !string.IsNullOrWhiteSpace(webhook.SecretToken))
            {
                hmacSignature = CalculateHmacSignature(payload, webhook.SecretToken);
                delivery.HmacSignature = hmacSignature;
            }

            // Prepare request
            var request = new HttpRequestMessage(HttpMethod.Post, webhook.Url);
            request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

            // Add headers
            request.Headers.Add(EventTypeHeader, delivery.EventType);
            request.Headers.Add(DeliveryIdHeader, delivery.Id.ToString());

            if (!string.IsNullOrWhiteSpace(correlationId))
                request.Headers.Add(CorrelationIdHeader, correlationId);

            if (hmacSignature != null)
                request.Headers.Add(HmacHeader, hmacSignature);

            // Add custom headers
            if (!string.IsNullOrWhiteSpace(webhook.CustomHeaders))
            {
                var customHeaders = JsonSerializer.Deserialize<Dictionary<string, string>>(webhook.CustomHeaders);
                if (customHeaders != null)
                {
                    foreach (var header in customHeaders)
                    {
                        request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                    }
                }
            }

            // Add OAuth2 token if configured
            if (webhook.AuthenticationType == "oauth2" && !string.IsNullOrWhiteSpace(webhook.OAuth2TokenUrl))
            {
                var token = await GetOAuth2TokenAsync(webhook);
                if (!string.IsNullOrWhiteSpace(token))
                    request.Headers.Add("Authorization", $"Bearer {token}");
            }

            // Send request
            var response = await httpClient.SendAsync(request);

            var responseBody = await response.Content.ReadAsStringAsync();
            var duration = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;

            delivery.ResponseStatus = (int)response.StatusCode;
            delivery.ResponseBody = responseBody.Length > 5000 ? responseBody.Substring(0, 5000) : responseBody;
            delivery.DurationMs = duration;

            if (response.IsSuccessStatusCode)
            {
                delivery.Status = "delivered";
                delivery.DeliveredAt = DateTime.UtcNow;
                delivery.ErrorMessage = null;

                _logger.LogInformation("Webhook delivered successfully: {WebhookId} -> {Url} ({Duration}ms)",
                    webhook.Id, webhook.Url, duration);

                return true;
            }
            else
            {
                delivery.ErrorMessage = $"HTTP {delivery.ResponseStatus}: {responseBody}";

                _logger.LogWarning("Webhook delivery failed: {WebhookId} -> {Url} - HTTP {Status}",
                    webhook.Id, webhook.Url, delivery.ResponseStatus);

                return false;
            }
        }
        catch (Exception ex)
        {
            var duration = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;
            delivery.DurationMs = duration;
            delivery.ErrorMessage = ex.Message;

            _logger.LogError(ex, "Webhook delivery exception: {WebhookId} -> {Url}",
                webhook.Id, webhook.Url);

            return false;
        }
        finally
        {
            await _context.SaveChangesAsync();
        }
    }

    private string CalculateHmacSignature(string payload, string secret)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);

        using var hmac = new HMACSHA256(keyBytes);
        var hashBytes = hmac.ComputeHash(payloadBytes);
        return $"sha256={Convert.ToHexString(hashBytes).ToLower()}";
    }

    private async Task<string?> GetOAuth2TokenAsync(Core.Models.Entities.Webhook webhook)
    {
        try
        {
            var httpClient = _httpClientFactory.CreateClient();

            var requestBody = new Dictionary<string, string>
            {
                { "grant_type", "client_credentials" },
                { "client_id", webhook.OAuth2ClientId! },
                { "client_secret", webhook.OAuth2ClientSecret! }
            };

            var response = await httpClient.PostAsync(webhook.OAuth2TokenUrl, new FormUrlEncodedContent(requestBody));

            if (!response.IsSuccessStatusCode)
                return null;

            var tokenResponse = await response.Content.ReadFromJsonAsync<OAuth2TokenResponse>();
            return tokenResponse?.AccessToken;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to obtain OAuth2 token for webhook {WebhookId}", webhook.Id);
            return null;
        }
    }

    private WebhookDto MapToDto(Core.Models.Entities.Webhook webhook)
    {
        return new WebhookDto
        {
            Id = webhook.Id,
            UserId = webhook.UserId,
            Name = webhook.Name,
            Url = webhook.Url,
            Description = webhook.Description,
            Events = webhook.Events,
            Active = webhook.Active,
            AuthenticationType = webhook.AuthenticationType,
            HasSecretToken = !string.IsNullOrWhiteSpace(webhook.SecretToken),
            CustomHeaders = !string.IsNullOrWhiteSpace(webhook.CustomHeaders)
                ? JsonSerializer.Deserialize<Dictionary<string, string>>(webhook.CustomHeaders)
                : null,
            MaxRetries = webhook.MaxRetries,
            TimeoutSeconds = webhook.TimeoutSeconds,
            VerifySsl = webhook.VerifySsl,
            CircuitBreakerState = webhook.CircuitBreakerState,
            ConsecutiveFailures = webhook.ConsecutiveFailures,
            TotalDeliveries = webhook.TotalDeliveries,
            SuccessfulDeliveries = webhook.SuccessfulDeliveries,
            FailedDeliveries = webhook.FailedDeliveries,
            LastTriggeredAt = webhook.LastTriggeredAt,
            LastSuccessAt = webhook.LastSuccessAt,
            LastFailureAt = webhook.LastFailureAt,
            CreatedAt = webhook.CreatedAt,
            UpdatedAt = webhook.UpdatedAt
        };
    }

    private WebhookDeliveryDto MapDeliveryToDto(WebhookDelivery delivery, string webhookName)
    {
        return new WebhookDeliveryDto
        {
            Id = delivery.Id,
            WebhookId = delivery.WebhookId,
            WebhookName = webhookName,
            EventType = delivery.EventType,
            Status = delivery.Status,
            AttemptCount = delivery.AttemptCount,
            ResponseStatus = delivery.ResponseStatus,
            ResponseBody = delivery.ResponseBody,
            ErrorMessage = delivery.ErrorMessage,
            DurationMs = delivery.DurationMs,
            CreatedAt = delivery.CreatedAt,
            DeliveredAt = delivery.DeliveredAt,
            NextRetryAt = delivery.NextRetryAt
        };
    }

    // Helper class for OAuth2 token response
    private class OAuth2TokenResponse
    {
        public string? AccessToken { get; set; }
        public string? TokenType { get; set; }
        public int ExpiresIn { get; set; }
    }
}
