using USP.Core.Models.DTOs.Authentication;

namespace USP.Core.Services.Authentication;

/// <summary>
/// Service for X.509 certificate-based authentication
/// </summary>
public interface ICertificateAuthService
{
    /// <summary>
    /// Authenticate user using X.509 client certificate
    /// </summary>
    Task<CertificateAuthResponse> AuthenticateWithCertificateAsync(CertificateAuthRequest request, string ipAddress, string userAgent);

    /// <summary>
    /// Enroll a new certificate for a user
    /// </summary>
    Task<EnrollCertificateResponse> EnrollCertificateAsync(EnrollCertificateRequest request);

    /// <summary>
    /// Get all certificates enrolled for a user
    /// </summary>
    Task<IEnumerable<UserCertificateDto>> GetUserCertificatesAsync(Guid userId);

    /// <summary>
    /// Revoke a user certificate
    /// </summary>
    Task<bool> RevokeCertificateAsync(Guid certificateId, string reason);

    /// <summary>
    /// Verify certificate is valid and not revoked
    /// </summary>
    Task<bool> VerifyCertificateAsync(string thumbprint);

    /// <summary>
    /// Check certificate revocation status (CRL/OCSP)
    /// </summary>
    Task<bool> CheckRevocationStatusAsync(string certificatePem);
}
