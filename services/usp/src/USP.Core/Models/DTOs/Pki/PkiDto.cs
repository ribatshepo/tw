using System.ComponentModel.DataAnnotations;

namespace USP.Core.Models.DTOs.Pki;

// ====================
// CA Management DTOs
// ====================

/// <summary>
/// Request to create a root Certificate Authority
/// </summary>
public class CreateRootCaRequest
{
    /// <summary>
    /// Unique CA name
    /// </summary>
    [Required(ErrorMessage = "CA name is required")]
    [StringLength(255, MinimumLength = 1, ErrorMessage = "CA name must be between 1 and 255 characters")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Subject Distinguished Name (e.g., "CN=My Root CA,O=MyOrg,C=US")
    /// </summary>
    [Required(ErrorMessage = "Subject DN is required")]
    [StringLength(500, MinimumLength = 1, ErrorMessage = "Subject DN must be between 1 and 500 characters")]
    public string SubjectDn { get; set; } = string.Empty;

    /// <summary>
    /// Key type: "rsa-2048", "rsa-4096", "ecdsa-p256", "ecdsa-p384"
    /// </summary>
    [Required]
    [RegularExpression("^(rsa-2048|rsa-4096|ecdsa-p256|ecdsa-p384)$", ErrorMessage = "Key type must be rsa-2048, rsa-4096, ecdsa-p256, or ecdsa-p384")]
    public string KeyType { get; set; } = "rsa-2048";

    /// <summary>
    /// Certificate lifetime in days
    /// </summary>
    [Range(1, 36500, ErrorMessage = "TTL must be between 1 and 36500 days")]
    public int TtlDays { get; set; } = 3650;

    /// <summary>
    /// Maximum CA chain depth (0 means no intermediates allowed)
    /// </summary>
    [Range(0, 10, ErrorMessage = "Max path length must be between 0 and 10")]
    public int MaxPathLength { get; set; } = 2;
}

/// <summary>
/// Request to create an intermediate Certificate Authority
/// </summary>
public class CreateIntermediateCaRequest
{
    /// <summary>
    /// Unique CA name
    /// </summary>
    [Required(ErrorMessage = "CA name is required")]
    [StringLength(255, MinimumLength = 1, ErrorMessage = "CA name must be between 1 and 255 characters")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Parent CA name that will sign this intermediate
    /// </summary>
    [Required(ErrorMessage = "Parent CA name is required")]
    [StringLength(255, MinimumLength = 1, ErrorMessage = "Parent CA name must be between 1 and 255 characters")]
    public string ParentCaName { get; set; } = string.Empty;

    /// <summary>
    /// Subject Distinguished Name (e.g., "CN=My Intermediate CA,O=MyOrg,C=US")
    /// </summary>
    [Required(ErrorMessage = "Subject DN is required")]
    [StringLength(500, MinimumLength = 1, ErrorMessage = "Subject DN must be between 1 and 500 characters")]
    public string SubjectDn { get; set; } = string.Empty;

    /// <summary>
    /// Key type: "rsa-2048", "rsa-4096", "ecdsa-p256", "ecdsa-p384"
    /// </summary>
    [Required]
    [RegularExpression("^(rsa-2048|rsa-4096|ecdsa-p256|ecdsa-p384)$", ErrorMessage = "Key type must be rsa-2048, rsa-4096, ecdsa-p256, or ecdsa-p384")]
    public string KeyType { get; set; } = "rsa-2048";

    /// <summary>
    /// Certificate lifetime in days
    /// </summary>
    [Range(1, 36500, ErrorMessage = "TTL must be between 1 and 36500 days")]
    public int TtlDays { get; set; } = 1825;

    /// <summary>
    /// Maximum CA chain depth (must be less than parent)
    /// </summary>
    [Range(0, 10, ErrorMessage = "Max path length must be between 0 and 10")]
    public int MaxPathLength { get; set; } = 1;
}

/// <summary>
/// Certificate Authority information response
/// </summary>
public class CertificateAuthorityResponse
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string SubjectDn { get; set; } = string.Empty;
    public string CertificatePem { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public string KeyType { get; set; } = string.Empty;
    public DateTime NotBefore { get; set; }
    public DateTime NotAfter { get; set; }
    public int MaxPathLength { get; set; }
    public string? ParentCaName { get; set; }
    public bool Revoked { get; set; }
    public DateTime? RevokedAt { get; set; }
    public long IssuedCertificateCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

// ====================
// Role Management DTOs
// ====================

/// <summary>
/// Request to create a certificate role/template
/// </summary>
public class CreateRoleRequest
{
    /// <summary>
    /// Unique role name
    /// </summary>
    [Required(ErrorMessage = "Role name is required")]
    [StringLength(255, MinimumLength = 1, ErrorMessage = "Role name must be between 1 and 255 characters")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// CA name that will issue certificates for this role
    /// </summary>
    [Required(ErrorMessage = "Certificate Authority name is required")]
    [StringLength(255, MinimumLength = 1, ErrorMessage = "CA name must be between 1 and 255 characters")]
    public string CertificateAuthorityName { get; set; } = string.Empty;

    /// <summary>
    /// Key type for issued certificates
    /// </summary>
    [Required]
    [RegularExpression("^(rsa-2048|rsa-4096|ecdsa-p256|ecdsa-p384)$", ErrorMessage = "Key type must be rsa-2048, rsa-4096, ecdsa-p256, or ecdsa-p384")]
    public string KeyType { get; set; } = "rsa-2048";

    /// <summary>
    /// Default certificate lifetime in days
    /// </summary>
    [Range(1, 36500, ErrorMessage = "TTL must be between 1 and 36500 days")]
    public int TtlDays { get; set; } = 365;

    /// <summary>
    /// Maximum allowed TTL in days
    /// </summary>
    [Range(1, 36500, ErrorMessage = "Max TTL must be between 1 and 36500 days")]
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
    /// Allowed domains for certificate issuance
    /// </summary>
    public List<string> AllowedDomains { get; set; } = new();

    /// <summary>
    /// TLS Web Server Authentication
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
}

/// <summary>
/// Certificate role information response
/// </summary>
public class RoleResponse
{
    public string Name { get; set; } = string.Empty;
    public string CertificateAuthorityName { get; set; } = string.Empty;
    public string KeyType { get; set; } = string.Empty;
    public int TtlDays { get; set; }
    public int MaxTtlDays { get; set; }
    public bool AllowLocalhost { get; set; }
    public bool AllowBareDomains { get; set; }
    public bool AllowSubdomains { get; set; }
    public bool AllowWildcards { get; set; }
    public bool AllowIpSans { get; set; }
    public List<string> AllowedDomains { get; set; } = new();
    public bool ServerAuth { get; set; }
    public bool ClientAuth { get; set; }
    public bool CodeSigning { get; set; }
    public bool EmailProtection { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

// ====================
// Certificate Issuance DTOs
// ====================

/// <summary>
/// Request to issue a certificate using a role
/// </summary>
public class IssueCertificateRequest
{
    /// <summary>
    /// Common Name for the certificate (e.g., "myserver.example.com")
    /// </summary>
    [Required(ErrorMessage = "Common Name is required")]
    [StringLength(255, MinimumLength = 1, ErrorMessage = "Common Name must be between 1 and 255 characters")]
    public string CommonName { get; set; } = string.Empty;

    /// <summary>
    /// Subject Alternative Names (DNS names, IP addresses)
    /// </summary>
    public List<string>? SubjectAltNames { get; set; }

    /// <summary>
    /// Override default TTL (must be within role's MaxTtlDays)
    /// </summary>
    [Range(1, 36500, ErrorMessage = "TTL must be between 1 and 36500 days")]
    public int? TtlDays { get; set; }
}

/// <summary>
/// Request to sign a Certificate Signing Request
/// </summary>
public class SignCsrRequest
{
    /// <summary>
    /// Certificate Signing Request in PEM format
    /// </summary>
    [Required(ErrorMessage = "CSR is required")]
    public string Csr { get; set; } = string.Empty;

    /// <summary>
    /// Override default TTL (must be within role's MaxTtlDays)
    /// </summary>
    [Range(1, 36500, ErrorMessage = "TTL must be between 1 and 36500 days")]
    public int? TtlDays { get; set; }
}

/// <summary>
/// Response from certificate issuance or CSR signing
/// </summary>
public class IssueCertificateResponse
{
    /// <summary>
    /// Issued certificate in PEM format
    /// </summary>
    public string CertificatePem { get; set; } = string.Empty;

    /// <summary>
    /// Private key in PEM format (only for IssueCertificate, not SignCsr)
    /// </summary>
    public string? PrivateKeyPem { get; set; }

    /// <summary>
    /// CA certificate chain in PEM format
    /// </summary>
    public string CaChainPem { get; set; } = string.Empty;

    /// <summary>
    /// Certificate serial number
    /// </summary>
    public string SerialNumber { get; set; } = string.Empty;

    /// <summary>
    /// Certificate validity dates
    /// </summary>
    public DateTime NotBefore { get; set; }
    public DateTime NotAfter { get; set; }

    /// <summary>
    /// Issuing CA name
    /// </summary>
    public string IssuingCa { get; set; } = string.Empty;
}

// ====================
// Certificate Operations DTOs
// ====================

/// <summary>
/// Request to revoke a certificate
/// </summary>
public class RevokeCertificateRequest
{
    /// <summary>
    /// Certificate serial number to revoke
    /// </summary>
    [Required(ErrorMessage = "Serial number is required")]
    [StringLength(255, MinimumLength = 1, ErrorMessage = "Serial number must be between 1 and 255 characters")]
    public string SerialNumber { get; set; } = string.Empty;
}

/// <summary>
/// Response from certificate revocation
/// </summary>
public class RevokeCertificateResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime RevokedAt { get; set; }
}

/// <summary>
/// Response with list of certificates
/// </summary>
public class ListCertificatesResponse
{
    public List<CertificateInfo> Certificates { get; set; } = new();
    public int TotalCount { get; set; }
}

/// <summary>
/// Certificate information
/// </summary>
public class CertificateInfo
{
    public string SerialNumber { get; set; } = string.Empty;
    public string SubjectDn { get; set; } = string.Empty;
    public string IssuingCa { get; set; } = string.Empty;
    public string? RoleName { get; set; }
    public DateTime NotBefore { get; set; }
    public DateTime NotAfter { get; set; }
    public bool Revoked { get; set; }
    public DateTime? RevokedAt { get; set; }
    public DateTime IssuedAt { get; set; }
}

// ====================
// CRL Management DTOs
// ====================

/// <summary>
/// Response with Certificate Revocation List
/// </summary>
public class GetCrlResponse
{
    /// <summary>
    /// CRL in PEM format
    /// </summary>
    public string Crl { get; set; } = string.Empty;

    /// <summary>
    /// CRL generation timestamp
    /// </summary>
    public DateTime GeneratedAt { get; set; }

    /// <summary>
    /// CRL expiration (Next Update timestamp)
    /// </summary>
    public DateTime NextUpdate { get; set; }

    /// <summary>
    /// Number of revoked certificates in this CRL
    /// </summary>
    public int RevokedCount { get; set; }
}
