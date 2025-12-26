namespace USP.Core.Models.Entities;

/// <summary>
/// Represents an issued end-entity certificate
/// Tracks all certificates issued by CAs in the PKI system
/// </summary>
public class PkiIssuedCertificate
{
    public Guid Id { get; set; }

    /// <summary>
    /// Unique certificate serial number in hexadecimal format
    /// </summary>
    public string SerialNumber { get; set; } = string.Empty;

    /// <summary>
    /// Certificate Authority that issued this certificate
    /// </summary>
    public Guid CertificateAuthorityId { get; set; }

    /// <summary>
    /// Navigation to the issuing CA
    /// </summary>
    public virtual PkiCertificateAuthority CertificateAuthority { get; set; } = null!;

    /// <summary>
    /// Role/template used for issuance (null if signed from CSR without role)
    /// </summary>
    public Guid? RoleId { get; set; }

    /// <summary>
    /// Navigation to the role
    /// </summary>
    public virtual PkiRole? Role { get; set; }

    // Certificate Details

    /// <summary>
    /// Subject Distinguished Name (e.g., "CN=myserver.example.com")
    /// </summary>
    public string SubjectDn { get; set; } = string.Empty;

    /// <summary>
    /// Public certificate in PEM format
    /// </summary>
    public string CertificatePem { get; set; } = string.Empty;

    /// <summary>
    /// Certificate validity start date
    /// </summary>
    public DateTime NotBefore { get; set; }

    /// <summary>
    /// Certificate validity end date
    /// </summary>
    public DateTime NotAfter { get; set; }

    // Revocation

    /// <summary>
    /// Whether this certificate has been revoked
    /// </summary>
    public bool Revoked { get; set; } = false;

    /// <summary>
    /// Timestamp when certificate was revoked
    /// </summary>
    public DateTime? RevokedAt { get; set; }

    // Audit Fields

    /// <summary>
    /// User who requested certificate issuance
    /// </summary>
    public Guid IssuedBy { get; set; }

    /// <summary>
    /// Timestamp when certificate was issued
    /// </summary>
    public DateTime IssuedAt { get; set; }
}
