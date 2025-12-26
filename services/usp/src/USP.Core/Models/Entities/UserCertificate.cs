namespace USP.Core.Models.Entities;

/// <summary>
/// User-enrolled X.509 certificate for authentication
/// </summary>
public class UserCertificate
{
    /// <summary>
    /// Certificate ID
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// User ID
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Friendly name for the certificate
    /// </summary>
    public string CertificateName { get; set; } = string.Empty;

    /// <summary>
    /// Certificate type (PIV, CAC, Personal, etc.)
    /// </summary>
    public string CertificateType { get; set; } = "Personal";

    /// <summary>
    /// Certificate thumbprint (SHA-256 hash)
    /// </summary>
    public string Thumbprint { get; set; } = string.Empty;

    /// <summary>
    /// Certificate serial number
    /// </summary>
    public string SerialNumber { get; set; } = string.Empty;

    /// <summary>
    /// Certificate subject DN
    /// </summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>
    /// Certificate issuer DN
    /// </summary>
    public string Issuer { get; set; } = string.Empty;

    /// <summary>
    /// Certificate public key in PEM format
    /// </summary>
    public string PublicKeyPem { get; set; } = string.Empty;

    /// <summary>
    /// Not valid before date
    /// </summary>
    public DateTime NotBefore { get; set; }

    /// <summary>
    /// Not valid after date
    /// </summary>
    public DateTime NotAfter { get; set; }

    /// <summary>
    /// Whether the certificate is active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Whether the certificate has been revoked
    /// </summary>
    public bool IsRevoked { get; set; }

    /// <summary>
    /// Revocation reason (if revoked)
    /// </summary>
    public string? RevocationReason { get; set; }

    /// <summary>
    /// When the certificate was revoked
    /// </summary>
    public DateTime? RevokedAt { get; set; }

    /// <summary>
    /// Last time this certificate was used for authentication
    /// </summary>
    public DateTime? LastUsedAt { get; set; }

    /// <summary>
    /// Total authentication count
    /// </summary>
    public int AuthenticationCount { get; set; }

    /// <summary>
    /// Enrollment timestamp
    /// </summary>
    public DateTime EnrolledAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last updated timestamp
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Navigation property
    /// </summary>
    public virtual ApplicationUser User { get; set; } = null!;
}
