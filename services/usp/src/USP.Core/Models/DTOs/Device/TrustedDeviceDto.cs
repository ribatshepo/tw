namespace USP.Core.Models.DTOs.Device;

/// <summary>
/// DTO for trusted device information
/// </summary>
public class TrustedDeviceDto
{
    public Guid Id { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public string DeviceType { get; set; } = string.Empty;
    public DateTime RegisteredAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public string? IpAddress { get; set; }
    public string? Location { get; set; }
}

/// <summary>
/// Request to register a trusted device
/// </summary>
public class RegisterTrustedDeviceRequest
{
    public string DeviceName { get; set; } = string.Empty;
    public Dictionary<string, string>? DeviceInfo { get; set; }
}

/// <summary>
/// Response after registering trusted device
/// </summary>
public class RegisterTrustedDeviceResponse
{
    public Guid DeviceId { get; set; }
    public string DeviceFingerprint { get; set; } = string.Empty;
    public DateTime RegisteredAt { get; set; }
    public int TrustDurationDays { get; set; } = 30;
}
