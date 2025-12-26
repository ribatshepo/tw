namespace USP.Core.Models.Entities;

/// <summary>
/// Represents a certificate template/role for issuing end-entity certificates
/// Defines certificate parameters and validation constraints
/// </summary>
public class PkiRole
{
    public Guid Id { get; set; }

    /// <summary>
    /// Unique role name for identification
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Certificate Authority that will issue certificates for this role
    /// </summary>
    public Guid CertificateAuthorityId { get; set; }

    /// <summary>
    /// Navigation to the issuing CA
    /// </summary>
    public virtual PkiCertificateAuthority CertificateAuthority { get; set; } = null!;

    // Certificate Configuration

    /// <summary>
    /// Key type for issued certificates: "rsa-2048", "rsa-4096", "ecdsa-p256", "ecdsa-p384"
    /// </summary>
    public string KeyType { get; set; } = "rsa-2048";

    /// <summary>
    /// Default certificate lifetime in days
    /// </summary>
    public int TtlDays { get; set; } = 365;

    /// <summary>
    /// Maximum allowed TTL in days
    /// </summary>
    public int MaxTtlDays { get; set; } = 3650;

    /// <summary>
    /// Allow certificates for localhost
    /// </summary>
    public bool AllowLocalhost { get; set; } = true;

    /// <summary>
    /// Allow bare domains (no subdomain)
    /// </summary>
    public bool AllowBareDomains { get; set; } = false;

    /// <summary>
    /// Allow subdomains of allowed domains
    /// </summary>
    public bool AllowSubdomains { get; set; } = false;

    /// <summary>
    /// Allow wildcard domains (*.example.com)
    /// </summary>
    public bool AllowWildcards { get; set; } = false;

    /// <summary>
    /// Allow IP addresses in Subject Alternative Names
    /// </summary>
    public bool AllowIpSans { get; set; } = true;

    /// <summary>
    /// Allowed domains for certificate issuance (JSON array)
    /// Example: ["example.com", "*.example.com"]
    /// </summary>
    public string AllowedDomains { get; set; } = "[]";

    // Key Usage Extensions

    /// <summary>
    /// TLS Web Server Authentication (typical for HTTPS servers)
    /// </summary>
    public bool ServerAuth { get; set; } = true;

    /// <summary>
    /// TLS Web Client Authentication
    /// </summary>
    public bool ClientAuth { get; set; } = false;

    /// <summary>
    /// Code Signing
    /// </summary>
    public bool CodeSigning { get; set; } = false;

    /// <summary>
    /// Email Protection (S/MIME)
    /// </summary>
    public bool EmailProtection { get; set; } = false;

    // Audit Fields

    /// <summary>
    /// User who created this role
    /// </summary>
    public Guid CreatedBy { get; set; }

    /// <summary>
    /// Timestamp when role was created
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Timestamp when role was last updated
    /// </summary>
    public DateTime? UpdatedAt { get; set; }
}
