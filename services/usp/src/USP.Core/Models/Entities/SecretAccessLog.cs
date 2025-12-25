using System.Net;

namespace USP.Core.Models.Entities;

/// <summary>
/// Audit log for secret access
/// </summary>
public class SecretAccessLog
{
    public Guid Id { get; set; }
    public Guid? SecretId { get; set; }
    public Guid? AccessedBy { get; set; }
    public string AccessType { get; set; } = string.Empty;
    public IPAddress? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime AccessedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual Secret? Secret { get; set; }
    public virtual ApplicationUser? Accessor { get; set; }
}
