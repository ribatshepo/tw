using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using USP.Core.Models.DTOs.Integrations;
using USP.Core.Services.Integrations;

namespace USP.Infrastructure.Integrations.SIEM;

/// <summary>
/// Elasticsearch integration using ECS (Elastic Common Schema)
/// </summary>
public class ElkIntegration : ISiemIntegration
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ElkIntegration> _logger;
    private readonly ElasticsearchSettings _settings;
    private readonly AsyncRetryPolicy _retryPolicy;

    public ElkIntegration(
        IHttpClientFactory httpClientFactory,
        IOptions<ElasticsearchSettings> settings,
        ILogger<ElkIntegration> logger)
    {
        _httpClient = httpClientFactory.CreateClient("Elasticsearch");
        _settings = settings.Value;
        _logger = logger;

        _retryPolicy = Policy
            .Handle<HttpRequestException>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (exception, timeSpan, retry, ctx) =>
                {
                    _logger.LogWarning(exception,
                        "Elasticsearch integration retry {Retry} after {Delay}s",
                        retry, timeSpan.TotalSeconds);
                });

        ConfigureHttpClient();
    }

    private void ConfigureHttpClient()
    {
        _httpClient.BaseAddress = new Uri(_settings.Url);

        if (!string.IsNullOrEmpty(_settings.Username) && !string.IsNullOrEmpty(_settings.Password))
        {
            var credentials = Convert.ToBase64String(
                Encoding.ASCII.GetBytes($"{_settings.Username}:{_settings.Password}"));
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Basic {credentials}");
        }

        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    public async Task SendAuditEventAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled)
        {
            _logger.LogDebug("Elasticsearch integration is disabled, skipping audit event");
            return;
        }

        try
        {
            var ecsEvent = BuildEcsEvent(auditEvent);
            var indexName = GetIndexName();

            await _retryPolicy.ExecuteAsync(async () =>
            {
                var json = JsonSerializer.Serialize(ecsEvent);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"/{indexName}/_doc", content, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError(
                        "Failed to send audit event to Elasticsearch. Status: {Status}, Response: {Response}",
                        response.StatusCode, errorContent);
                    throw new HttpRequestException($"Elasticsearch returned status {response.StatusCode}");
                }

                _logger.LogInformation(
                    "Sent audit event to Elasticsearch: {EventType}, User: {Username}",
                    auditEvent.EventType, auditEvent.Username);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending audit event to Elasticsearch");
            throw;
        }
    }

    public async Task SendSecurityAlertAsync(SecurityAlert alert, CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled)
        {
            _logger.LogDebug("Elasticsearch integration is disabled, skipping security alert");
            return;
        }

        try
        {
            var ecsAlert = BuildEcsAlertEvent(alert);
            var indexName = $"{_settings.IndexPrefix}-alerts-{DateTime.UtcNow:yyyy.MM.dd}";

            await _retryPolicy.ExecuteAsync(async () =>
            {
                var json = JsonSerializer.Serialize(ecsAlert);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"/{indexName}/_doc", content, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError(
                        "Failed to send security alert to Elasticsearch. Status: {Status}, Response: {Response}",
                        response.StatusCode, errorContent);
                    throw new HttpRequestException($"Elasticsearch returned status {response.StatusCode}");
                }

                _logger.LogInformation(
                    "Sent security alert to Elasticsearch: {Severity} - {Title}",
                    alert.Severity, alert.Title);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending security alert to Elasticsearch");
            throw;
        }
    }

    public async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled)
        {
            return false;
        }

        try
        {
            var response = await _httpClient.GetAsync("/_cluster/health", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Elasticsearch health check failed");
            return false;
        }
    }

    public string GetIntegrationName() => "Elasticsearch";

    private string GetIndexName()
    {
        return $"{_settings.IndexPrefix}-{DateTime.UtcNow:yyyy.MM.dd}";
    }

    private object BuildEcsEvent(AuditEvent auditEvent)
    {
        // Build ECS (Elastic Common Schema) compliant event
        return new
        {
            @timestamp = auditEvent.Timestamp,
            ecs = new { version = "8.0.0" },
            @event = new
            {
                id = auditEvent.EventId,
                kind = "event",
                category = new[] { "authentication", "audit" },
                type = new[] { "info" },
                action = auditEvent.Action,
                outcome = auditEvent.Success ? "success" : "failure",
                severity = MapSeverityToEcs(auditEvent.Severity)
            },
            user = new
            {
                id = auditEvent.UserId,
                name = auditEvent.Username
            },
            source = new
            {
                ip = auditEvent.IpAddress
            },
            user_agent = new
            {
                original = auditEvent.UserAgent
            },
            resource = new
            {
                type = auditEvent.ResourceType,
                id = auditEvent.ResourceId
            },
            error = new
            {
                message = auditEvent.ErrorMessage
            },
            labels = new
            {
                event_type = auditEvent.EventType,
                correlation_id = auditEvent.CorrelationId,
                source_system = auditEvent.Source
            },
            metadata = auditEvent.Metadata
        };
    }

    private object BuildEcsAlertEvent(SecurityAlert alert)
    {
        return new
        {
            @timestamp = alert.Timestamp,
            ecs = new { version = "8.0.0" },
            @event = new
            {
                id = alert.Id,
                kind = "alert",
                category = new[] { "security" },
                type = new[] { "indicator" },
                severity = MapSeverityToEcs(alert.Severity)
            },
            rule = new
            {
                name = alert.Title,
                description = alert.Description
            },
            threat = new
            {
                indicator = new
                {
                    type = alert.Type
                }
            },
            user = new
            {
                name = alert.User
            },
            source = new
            {
                ip = alert.IpAddress
            },
            resource = new
            {
                name = alert.Resource
            },
            labels = new
            {
                correlation_id = alert.CorrelationId,
                source_system = alert.Source
            },
            metadata = alert.Metadata
        };
    }

    private int MapSeverityToEcs(string severity)
    {
        return severity switch
        {
            AlertSeverity.Critical => 4,
            AlertSeverity.High => 3,
            AlertSeverity.Medium => 2,
            AlertSeverity.Low => 1,
            _ => 0
        };
    }
}
