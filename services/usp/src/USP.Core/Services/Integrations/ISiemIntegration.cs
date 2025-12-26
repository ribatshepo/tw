using USP.Core.Models.DTOs.Integrations;

namespace USP.Core.Services.Integrations;

/// <summary>
/// Interface for SIEM integrations
/// </summary>
public interface ISiemIntegration
{
    /// <summary>
    /// Send audit event to SIEM platform
    /// </summary>
    Task SendAuditEventAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Send security alert to SIEM platform
    /// </summary>
    Task SendSecurityAlertAsync(SecurityAlert alert, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if SIEM integration is healthy
    /// </summary>
    Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get integration name
    /// </summary>
    string GetIntegrationName();
}
