using USP.Core.Models.DTOs.Integrations;

namespace USP.Core.Services.Integrations;

/// <summary>
/// Interface for ticketing system integrations (Jira, ServiceNow)
/// </summary>
public interface ITicketingService
{
    /// <summary>
    /// Create security incident ticket
    /// </summary>
    Task<string> CreateIncidentAsync(SecurityAlert alert, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update incident ticket status
    /// </summary>
    Task UpdateIncidentAsync(string ticketId, string status, string? comment = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Attach evidence to incident ticket
    /// </summary>
    Task AttachEvidenceAsync(string ticketId, string fileName, byte[] content, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get incident ticket details
    /// </summary>
    Task<object> GetIncidentAsync(string ticketId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get service name
    /// </summary>
    string GetServiceName();
}
