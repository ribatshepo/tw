namespace USP.Core.Models.Entities;

/// <summary>
/// Registered MFA device/authenticator
/// </summary>
public class MfaDevice
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string DeviceType { get; set; } = string.Empty; // TOTP, SMS, Email
    public string DeviceName { get; set; } = string.Empty;
    public string? DeviceFingerprint { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsPrimary { get; set; }
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
    public DateTime EnrolledAt { get; set; } = DateTime.UtcNow; // Alias for RegisteredAt
    public DateTime? LastUsedAt { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }

    // Navigation properties
    public virtual ApplicationUser User { get; set; } = null!;
}
