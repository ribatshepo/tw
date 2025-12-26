using USP.Core.Models.DTOs.Integrations;

namespace USP.Core.Services.Integrations;

/// <summary>
/// Interface for notification services (Slack, Teams, PagerDuty)
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Send security alert notification
    /// </summary>
    Task SendAlertAsync(SecurityAlert alert, CancellationToken cancellationToken = default);

    /// <summary>
    /// Send custom notification message
    /// </summary>
    Task SendNotificationAsync(string title, string message, string? severity = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if notification service is healthy
    /// </summary>
    Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get service name
    /// </summary>
    string GetServiceName();
}
