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
/// Datadog API integration for security events
/// </summary>
public class DatadogIntegration : ISiemIntegration
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<DatadogIntegration> _logger;
    private readonly DatadogSettings _settings;
    private readonly AsyncRetryPolicy _retryPolicy;

    public DatadogIntegration(
        IHttpClientFactory httpClientFactory,
        IOptions<DatadogSettings> settings,
        ILogger<DatadogIntegration> logger)
    {
        _httpClient = httpClientFactory.CreateClient("Datadog");
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
                        "Datadog integration retry {Retry} after {Delay}s",
                        retry, timeSpan.TotalSeconds);
                });

        ConfigureHttpClient();
    }

    private void ConfigureHttpClient()
    {
        _httpClient.BaseAddress = new Uri($"https://api.{_settings.Site}");
        _httpClient.DefaultRequestHeaders.Add("DD-API-KEY", _settings.ApiKey);
        _httpClient.DefaultRequestHeaders.Add("DD-APPLICATION-KEY", _settings.ApplicationKey);
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    public async Task SendAuditEventAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled)
        {
            _logger.LogDebug("Datadog integration is disabled, skipping audit event");
            return;
        }

        try
        {
            var datadogEvent = BuildDatadogEvent(auditEvent);

            await _retryPolicy.ExecuteAsync(async () =>
            {
                var json = JsonSerializer.Serialize(datadogEvent);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("/api/v1/events", content, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError(
                        "Failed to send audit event to Datadog. Status: {Status}, Response: {Response}",
                        response.StatusCode, errorContent);
                    throw new HttpRequestException($"Datadog returned status {response.StatusCode}");
                }

                _logger.LogInformation(
                    "Sent audit event to Datadog: {EventType}, User: {Username}",
                    auditEvent.EventType, auditEvent.Username);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending audit event to Datadog");
            throw;
        }
    }

    public async Task SendSecurityAlertAsync(SecurityAlert alert, CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled)
        {
            _logger.LogDebug("Datadog integration is disabled, skipping security alert");
            return;
        }

        try
        {
            var datadogEvent = BuildDatadogAlertEvent(alert);

            await _retryPolicy.ExecuteAsync(async () =>
            {
                var json = JsonSerializer.Serialize(datadogEvent);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("/api/v1/events", content, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError(
                        "Failed to send security alert to Datadog. Status: {Status}, Response: {Response}",
                        response.StatusCode, errorContent);
                    throw new HttpRequestException($"Datadog returned status {response.StatusCode}");
                }

                _logger.LogInformation(
                    "Sent security alert to Datadog: {Severity} - {Title}",
                    alert.Severity, alert.Title);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending security alert to Datadog");
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
            var response = await _httpClient.GetAsync("/api/v1/validate", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Datadog health check failed");
            return false;
        }
    }

    public string GetIntegrationName() => "Datadog";

    private object BuildDatadogEvent(AuditEvent auditEvent)
    {
        var priority = auditEvent.Severity.ToLower() switch
        {
            "critical" => "high",
            "high" => "high",
            "medium" => "normal",
            _ => "low"
        };

        return new
        {
            title = $"USP Audit: {auditEvent.EventType}",
            text = $"Action: {auditEvent.Action}, User: {auditEvent.Username}, Resource: {auditEvent.ResourceType}",
            priority = priority,
            tags = new[]
            {
                $"event_type:{auditEvent.EventType}",
                $"action:{auditEvent.Action}",
                $"user_id:{auditEvent.UserId}",
                $"resource_type:{auditEvent.ResourceType}",
                $"success:{auditEvent.Success}",
                $"severity:{auditEvent.Severity}",
                "source:usp"
            },
            alert_type = auditEvent.Success ? "info" : "error",
            source_type_name = "usp"
        };
    }

    private object BuildDatadogAlertEvent(SecurityAlert alert)
    {
        var alertType = alert.Severity.ToLower() switch
        {
            "critical" => "error",
            "high" => "error",
            "medium" => "warning",
            _ => "info"
        };

        return new
        {
            title = alert.Title,
            text = alert.Description,
            priority = alert.Severity.ToLower() == "critical" || alert.Severity.ToLower() == "high" ? "high" : "normal",
            tags = new[]
            {
                $"alert_type:{alert.Type}",
                $"severity:{alert.Severity}",
                $"user:{alert.User}",
                $"resource:{alert.Resource}",
                "source:usp"
            },
            alert_type = alertType,
            source_type_name = "usp"
        };
    }
}
