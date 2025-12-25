namespace USP.Core.Models.Entities;

/// <summary>
/// Represents a trusted device for a user
/// </summary>
public class TrustedDevice
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string DeviceFingerprint { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public string DeviceType { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public string? Location { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastUsedAt { get; set; }
    public DateTime ExpiresAt { get; set; }

    // Navigation property
    public virtual ApplicationUser User { get; set; } = null!;
}
