using System.Net;
using System.Text.Json;

namespace USP.Core.Models.Entities;

/// <summary>
/// Comprehensive audit log entity
/// </summary>
public class AuditLog
{
    public Guid Id { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public Guid? UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Resource { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public string? ResourceId { get; set; }
    public string IpAddress { get; set; } = string.Empty;
    public string? UserAgent { get; set; }
    public string HttpMethod { get; set; } = string.Empty;
    public string RequestPath { get; set; } = string.Empty;
    public string? QueryString { get; set; }
    public string? RequestBody { get; set; }
    public int ResponseStatus { get; set; }
    public string? ResponseBody { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public int DurationMs { get; set; }
    public string? Metadata { get; set; }
    public JsonDocument? OldValue { get; set; }
    public JsonDocument? NewValue { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual ApplicationUser? User { get; set; }
}
