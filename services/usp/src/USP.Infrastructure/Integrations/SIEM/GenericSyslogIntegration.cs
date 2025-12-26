using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging;
using USP.Core.Models.DTOs.Integrations;
using USP.Core.Services.Integrations;

namespace USP.Infrastructure.Integrations.SIEM;

/// <summary>
/// Generic Syslog integration (RFC 5424/5425 compliant)
/// </summary>
public class GenericSyslogIntegration : ISiemIntegration
{
    private readonly ILogger<GenericSyslogIntegration> _logger;
    private readonly string _syslogHost;
    private readonly int _syslogPort;
    private readonly string _facility;
    private readonly bool _enabled;
    private readonly bool _useTcp;

    public GenericSyslogIntegration(
        ILogger<GenericSyslogIntegration> logger,
        string syslogHost = "localhost",
        int syslogPort = 514,
        string facility = "local0",
        bool enabled = false,
        bool useTcp = false)
    {
        _logger = logger;
        _syslogHost = syslogHost;
        _syslogPort = syslogPort;
        _facility = facility;
        _enabled = enabled;
        _useTcp = useTcp;
    }

    public async Task SendAuditEventAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        if (!_enabled)
        {
            _logger.LogDebug("Syslog integration is disabled, skipping audit event");
            return;
        }

        try
        {
            var syslogMessage = BuildSyslogMessage(auditEvent);
            await SendSyslogMessageAsync(syslogMessage, cancellationToken);

            _logger.LogInformation(
                "Sent audit event to Syslog: {EventType}, User: {Username}",
                auditEvent.EventType, auditEvent.Username);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending audit event to Syslog");
            throw;
        }
    }

    public async Task SendSecurityAlertAsync(SecurityAlert alert, CancellationToken cancellationToken = default)
    {
        if (!_enabled)
        {
            _logger.LogDebug("Syslog integration is disabled, skipping security alert");
            return;
        }

        try
        {
            var syslogMessage = BuildSyslogAlertMessage(alert);
            await SendSyslogMessageAsync(syslogMessage, cancellationToken);

            _logger.LogInformation(
                "Sent security alert to Syslog: {Severity} - {Title}",
                alert.Severity, alert.Title);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending security alert to Syslog");
            throw;
        }
    }

    public Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        if (!_enabled)
        {
            return Task.FromResult(false);
        }

        try
        {
            using var client = new TcpClient();
            var connectTask = client.ConnectAsync(_syslogHost, _syslogPort);
            return Task.FromResult(connectTask.Wait(TimeSpan.FromSeconds(5)));
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    public string GetIntegrationName() => "Syslog";

    private string BuildSyslogMessage(AuditEvent auditEvent)
    {
        var severity = GetSyslogSeverity(auditEvent.Severity);
        var facilityCode = GetFacilityCode(_facility);
        var priority = (facilityCode * 8) + severity;

        var timestamp = auditEvent.Timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        var hostname = System.Environment.MachineName;
        var appName = "USP";
        var procId = System.Diagnostics.Process.GetCurrentProcess().Id;
        var msgId = auditEvent.EventType;

        var message = $"{auditEvent.Action} by {auditEvent.Username} on {auditEvent.ResourceType} " +
                     $"(Result: {(auditEvent.Success ? "Success" : "Failure")})";

        return $"<{priority}>1 {timestamp} {hostname} {appName} {procId} {msgId} - {message}";
    }

    private string BuildSyslogAlertMessage(SecurityAlert alert)
    {
        var severity = GetSyslogSeverity(alert.Severity);
        var facilityCode = GetFacilityCode(_facility);
        var priority = (facilityCode * 8) + severity;

        var timestamp = alert.Timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        var hostname = System.Environment.MachineName;
        var appName = "USP";
        var procId = System.Diagnostics.Process.GetCurrentProcess().Id;
        var msgId = alert.Type;

        var message = $"[{alert.Severity}] {alert.Title}: {alert.Description}";
        if (!string.IsNullOrEmpty(alert.User))
        {
            message += $" (User: {alert.User})";
        }

        return $"<{priority}>1 {timestamp} {hostname} {appName} {procId} {msgId} - {message}";
    }

    private async Task SendSyslogMessageAsync(string message, CancellationToken cancellationToken)
    {
        var messageBytes = Encoding.UTF8.GetBytes(message);

        if (_useTcp)
        {
            using var client = new TcpClient();
            await client.ConnectAsync(_syslogHost, _syslogPort, cancellationToken);
            using var stream = client.GetStream();
            await stream.WriteAsync(messageBytes, 0, messageBytes.Length, cancellationToken);
        }
        else
        {
            using var client = new UdpClient();
            await client.SendAsync(messageBytes, messageBytes.Length, _syslogHost, _syslogPort);
        }
    }

    private int GetSyslogSeverity(string severity)
    {
        return severity.ToLower() switch
        {
            "critical" => 2,  // Critical
            "high" => 3,      // Error
            "medium" => 4,    // Warning
            "low" => 5,       // Notice
            "info" => 6,      // Informational
            _ => 7            // Debug
        };
    }

    private int GetFacilityCode(string facility)
    {
        return facility.ToLower() switch
        {
            "kern" => 0,
            "user" => 1,
            "mail" => 2,
            "daemon" => 3,
            "auth" => 4,
            "syslog" => 5,
            "lpr" => 6,
            "news" => 7,
            "uucp" => 8,
            "cron" => 9,
            "authpriv" => 10,
            "ftp" => 11,
            "local0" => 16,
            "local1" => 17,
            "local2" => 18,
            "local3" => 19,
            "local4" => 20,
            "local5" => 21,
            "local6" => 22,
            "local7" => 23,
            _ => 16  // default to local0
        };
    }
}
