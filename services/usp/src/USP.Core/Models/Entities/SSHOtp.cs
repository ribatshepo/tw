namespace USP.Core.Models.Entities;

/// <summary>
/// SSH One-Time Password for temporary access
/// </summary>
public class SSHOtp
{
    public Guid Id { get; set; }
    public string Otp { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Hostname { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public bool IsUsed { get; set; }
    public DateTime? UsedAt { get; set; }
    public string? UsedFromIp { get; set; }
    public Guid IssuedBy { get; set; }
    public DateTime IssuedAt { get; set; }
}
