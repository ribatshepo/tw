namespace USP.Core.Models.Entities;

/// <summary>
/// SSH role configuration for certificate signing
/// </summary>
public class SSHRole
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string KeyType { get; set; } = "rsa";
    public int DefaultTtlSeconds { get; set; } = 3600;
    public int MaxTtlSeconds { get; set; } = 86400;
    public string AllowedPrincipals { get; set; } = string.Empty;
    public string CertificateType { get; set; } = "user";
    public bool AllowUserCertificates { get; set; } = true;
    public bool AllowHostCertificates { get; set; } = false;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid CreatedBy { get; set; }
}
