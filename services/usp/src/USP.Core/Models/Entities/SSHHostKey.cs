namespace USP.Core.Models.Entities;

/// <summary>
/// Trusted SSH host key for host verification
/// </summary>
public class SSHHostKey
{
    public Guid Id { get; set; }
    public string Hostname { get; set; } = string.Empty;
    public string PublicKey { get; set; } = string.Empty;
    public string KeyType { get; set; } = string.Empty;
    public string Fingerprint { get; set; } = string.Empty;
    public DateTime RegisteredAt { get; set; }
    public Guid RegisteredBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}
