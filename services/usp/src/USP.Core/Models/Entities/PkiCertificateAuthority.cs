namespace USP.Core.Models.Entities;

/// <summary>
/// Represents a Certificate Authority (CA) in the PKI system
/// Stores both root and intermediate CAs with encrypted private keys
/// </summary>
public class PkiCertificateAuthority
{
    public Guid Id { get; set; }

    /// <summary>
    /// Unique CA name for identification
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// CA type: "root" or "intermediate"
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Subject Distinguished Name (e.g., "CN=My Root CA,O=MyOrg,C=US")
    /// </summary>
    public string SubjectDn { get; set; } = string.Empty;

    /// <summary>
    /// Public certificate in PEM format
    /// </summary>
    public string CertificatePem { get; set; } = string.Empty;

    /// <summary>
    /// Certificate serial number in hexadecimal format
    /// </summary>
    public string SerialNumber { get; set; } = string.Empty;

    /// <summary>
    /// CA private key encrypted with master key
    /// CRITICAL: Never expose this field in APIs
    /// </summary>
    public string EncryptedPrivateKey { get; set; } = string.Empty;

    /// <summary>
    /// Key type: "rsa-2048", "rsa-4096", "ecdsa-p256", "ecdsa-p384"
    /// </summary>
    public string KeyType { get; set; } = string.Empty;

    /// <summary>
    /// Certificate validity start date
    /// </summary>
    public DateTime NotBefore { get; set; }

    /// <summary>
    /// Certificate validity end date
    /// </summary>
    public DateTime NotAfter { get; set; }

    /// <summary>
    /// Maximum CA chain depth constraint
    /// Enforces how many intermediate CAs can be created from this CA
    /// </summary>
    public int MaxPathLength { get; set; } = 0;

    // CA Hierarchy

    /// <summary>
    /// Parent CA ID (null for root CA, set for intermediate CA)
    /// </summary>
    public Guid? ParentCaId { get; set; }

    /// <summary>
    /// Parent CA entity for intermediate CAs
    /// </summary>
    public virtual PkiCertificateAuthority? ParentCa { get; set; }

    // Status and Statistics

    /// <summary>
    /// Whether this CA has been revoked
    /// </summary>
    public bool Revoked { get; set; } = false;

    /// <summary>
    /// Timestamp when CA was revoked
    /// </summary>
    public DateTime? RevokedAt { get; set; }

    /// <summary>
    /// Counter for total certificates issued by this CA
    /// </summary>
    public long IssuedCertificateCount { get; set; } = 0;

    // Audit Fields

    /// <summary>
    /// User who created this CA
    /// </summary>
    public Guid CreatedBy { get; set; }

    /// <summary>
    /// Timestamp when CA was created
    /// </summary>
    public DateTime CreatedAt { get; set; }

    // Navigation Properties

    /// <summary>
    /// Certificates issued by this CA
    /// </summary>
    public virtual ICollection<PkiIssuedCertificate> IssuedCertificates { get; set; } = new List<PkiIssuedCertificate>();
}
