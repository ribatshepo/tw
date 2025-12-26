namespace USP.Core.Models.Entities;

/// <summary>
/// Signed SSH certificate record
/// </summary>
public class SSHCertificate
{
    public Guid Id { get; set; }
    public string SerialNumber { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public string PublicKey { get; set; } = string.Empty;
    public string SignedCertificate { get; set; } = string.Empty;
    public DateTime ValidAfter { get; set; }
    public DateTime ValidBefore { get; set; }
    public string ValidPrincipals { get; set; } = string.Empty;
    public string CertificateType { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public DateTime CreatedAt { get; set; }
}
