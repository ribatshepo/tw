namespace USP.Core.Models.DTOs.Authentication;

/// <summary>
/// Request for certificate-based authentication
/// </summary>
public class CertificateAuthRequest
{
    /// <summary>
    /// X.509 certificate in PEM format
    /// </summary>
    public string CertificatePem { get; set; } = string.Empty;

    /// <summary>
    /// Optional challenge signed with the certificate private key
    /// </summary>
    public string? SignedChallenge { get; set; }

    /// <summary>
    /// Device fingerprint for trust evaluation
    /// </summary>
    public string? DeviceFingerprint { get; set; }
}

/// <summary>
/// Response for certificate authentication
/// </summary>
public class CertificateAuthResponse
{
    /// <summary>
    /// Whether authentication was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Access token (if successful)
    /// </summary>
    public string? AccessToken { get; set; }

    /// <summary>
    /// Refresh token (if successful)
    /// </summary>
    public string? RefreshToken { get; set; }

    /// <summary>
    /// Token expiration time
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Error message (if failed)
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Certificate subject name
    /// </summary>
    public string? CertificateSubject { get; set; }

    /// <summary>
    /// Certificate serial number
    /// </summary>
    public string? CertificateSerial { get; set; }
}

/// <summary>
/// Request to enroll a certificate for a user
/// </summary>
public class EnrollCertificateRequest
{
    /// <summary>
    /// User ID
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// X.509 certificate in PEM format
    /// </summary>
    public string CertificatePem { get; set; } = string.Empty;

    /// <summary>
    /// Friendly name for the certificate
    /// </summary>
    public string CertificateName { get; set; } = string.Empty;

    /// <summary>
    /// Certificate type (e.g., "PIV", "CAC", "Personal")
    /// </summary>
    public string CertificateType { get; set; } = "Personal";
}

/// <summary>
/// Response for certificate enrollment
/// </summary>
public class EnrollCertificateResponse
{
    /// <summary>
    /// Enrolled certificate ID
    /// </summary>
    public Guid CertificateId { get; set; }

    /// <summary>
    /// Certificate thumbprint
    /// </summary>
    public string Thumbprint { get; set; } = string.Empty;

    /// <summary>
    /// Certificate subject
    /// </summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>
    /// Certificate issuer
    /// </summary>
    public string Issuer { get; set; } = string.Empty;

    /// <summary>
    /// Certificate expiration date
    /// </summary>
    public DateTime NotAfter { get; set; }

    /// <summary>
    /// Enrollment timestamp
    /// </summary>
    public DateTime EnrolledAt { get; set; }
}

/// <summary>
/// DTO for user-enrolled certificate
/// </summary>
public class UserCertificateDto
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
    /// Certificate name
    /// </summary>
    public string CertificateName { get; set; } = string.Empty;

    /// <summary>
    /// Certificate type
    /// </summary>
    public string CertificateType { get; set; } = string.Empty;

    /// <summary>
    /// Certificate thumbprint
    /// </summary>
    public string Thumbprint { get; set; } = string.Empty;

    /// <summary>
    /// Certificate subject
    /// </summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>
    /// Certificate issuer
    /// </summary>
    public string Issuer { get; set; } = string.Empty;

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
    public bool IsActive { get; set; }

    /// <summary>
    /// Whether the certificate is revoked
    /// </summary>
    public bool IsRevoked { get; set; }

    /// <summary>
    /// Last used timestamp
    /// </summary>
    public DateTime? LastUsedAt { get; set; }

    /// <summary>
    /// Enrollment timestamp
    /// </summary>
    public DateTime EnrolledAt { get; set; }
}
