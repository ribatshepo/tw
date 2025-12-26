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
/// Splunk HTTP Event Collector (HEC) integration
/// </summary>
public class SplunkIntegration : ISiemIntegration
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SplunkIntegration> _logger;
    private readonly SplunkSettings _settings;
    private readonly AsyncRetryPolicy _retryPolicy;

    public SplunkIntegration(
        IHttpClientFactory httpClientFactory,
        IOptions<SplunkSettings> settings,
        ILogger<SplunkIntegration> logger)
    {
        _httpClient = httpClientFactory.CreateClient("Splunk");
        _settings = settings.Value;
        _logger = logger;

        // Configure retry policy with exponential backoff
        _retryPolicy = Policy
            .Handle<HttpRequestException>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (exception, timeSpan, retry, ctx) =>
                {
                    _logger.LogWarning(exception,
                        "Splunk integration retry {Retry} after {Delay}s",
                        retry, timeSpan.TotalSeconds);
                });

        ConfigureHttpClient();
    }

    private void ConfigureHttpClient()
    {
        _httpClient.BaseAddress = new Uri(_settings.HecUrl);
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Splunk {_settings.HecToken}");
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    public async Task SendAuditEventAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled)
        {
            _logger.LogDebug("Splunk integration is disabled, skipping audit event");
            return;
        }

        try
        {
            var splunkEvent = BuildSplunkEvent(auditEvent);

            await _retryPolicy.ExecuteAsync(async () =>
            {
                var json = JsonSerializer.Serialize(splunkEvent);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("/services/collector/event", content, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError(
                        "Failed to send audit event to Splunk. Status: {Status}, Response: {Response}",
                        response.StatusCode, errorContent);
                    throw new HttpRequestException($"Splunk returned status {response.StatusCode}");
                }

                _logger.LogInformation(
                    "Sent audit event to Splunk: {EventType}, User: {Username}",
                    auditEvent.EventType, auditEvent.Username);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending audit event to Splunk");
            throw;
        }
    }

    public async Task SendSecurityAlertAsync(SecurityAlert alert, CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled)
        {
            _logger.LogDebug("Splunk integration is disabled, skipping security alert");
            return;
        }

        try
        {
            var splunkEvent = BuildSplunkAlertEvent(alert);

            await _retryPolicy.ExecuteAsync(async () =>
            {
                var json = JsonSerializer.Serialize(splunkEvent);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("/services/collector/event", content, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError(
                        "Failed to send security alert to Splunk. Status: {Status}, Response: {Response}",
                        response.StatusCode, errorContent);
                    throw new HttpRequestException($"Splunk returned status {response.StatusCode}");
                }

                _logger.LogInformation(
                    "Sent security alert to Splunk: {Severity} - {Title}",
                    alert.Severity, alert.Title);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending security alert to Splunk");
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
            var response = await _httpClient.GetAsync("/services/collector/health", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Splunk health check failed");
            return false;
        }
    }

    public string GetIntegrationName() => "Splunk";

    private object BuildSplunkEvent(AuditEvent auditEvent)
    {
        // Build Splunk CIM (Common Information Model) compliant event
        return new
        {
            time = new DateTimeOffset(auditEvent.Timestamp).ToUnixTimeSeconds(),
            source = _settings.Source,
            sourcetype = _settings.SourceType,
            index = _settings.Index,
            @event = new
            {
                event_id = auditEvent.EventId,
                timestamp = auditEvent.Timestamp,
                event_type = auditEvent.EventType,
                action = auditEvent.Action,
                user_id = auditEvent.UserId,
                username = auditEvent.Username,
                resource_type = auditEvent.ResourceType,
                resource_id = auditEvent.ResourceId,
                src_ip = auditEvent.IpAddress,
                user_agent = auditEvent.UserAgent,
                success = auditEvent.Success,
                error_message = auditEvent.ErrorMessage,
                severity = auditEvent.Severity,
                correlation_id = auditEvent.CorrelationId,
                metadata = auditEvent.Metadata,
                source_system = auditEvent.Source
            }
        };
    }

    private object BuildSplunkAlertEvent(SecurityAlert alert)
    {
        return new
        {
            time = new DateTimeOffset(alert.Timestamp).ToUnixTimeSeconds(),
            source = _settings.Source,
            sourcetype = "usp:security_alert",
            index = _settings.Index,
            @event = new
            {
                alert_id = alert.Id,
                timestamp = alert.Timestamp,
                alert_type = alert.Type,
                severity = alert.Severity,
                title = alert.Title,
                description = alert.Description,
                user = alert.User,
                src_ip = alert.IpAddress,
                resource = alert.Resource,
                correlation_id = alert.CorrelationId,
                metadata = alert.Metadata,
                source_system = alert.Source
            }
        };
    }
}
