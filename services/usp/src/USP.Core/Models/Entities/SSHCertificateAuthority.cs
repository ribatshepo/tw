namespace USP.Core.Models.Entities;

/// <summary>
/// SSH Certificate Authority key pair
/// </summary>
public class SSHCertificateAuthority
{
    public Guid Id { get; set; }
    public string KeyType { get; set; } = string.Empty;
    public string PublicKey { get; set; } = string.Empty;
    public string PrivateKey { get; set; } = string.Empty; // Encrypted
    public string Fingerprint { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public Guid CreatedBy { get; set; }
}
