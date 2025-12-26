using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using USP.Core.Models.DTOs.Authentication;
using USP.Core.Models.Entities;
using USP.Core.Services.Authentication;
using USP.Infrastructure.Data;

namespace USP.Infrastructure.Services.Authentication;

/// <summary>
/// Service for X.509 certificate-based authentication
/// </summary>
public class CertificateAuthService : ICertificateAuthService
{
    private readonly ApplicationDbContext _context;
    private readonly IJwtService _jwtService;
    private readonly ILogger<CertificateAuthService> _logger;
    private readonly IRiskAssessmentService _riskService;

    public CertificateAuthService(
        ApplicationDbContext context,
        IJwtService jwtService,
        ILogger<CertificateAuthService> logger,
        IRiskAssessmentService riskService)
    {
        _context = context;
        _jwtService = jwtService;
        _logger = logger;
        _riskService = riskService;
    }

    public async Task<CertificateAuthResponse> AuthenticateWithCertificateAsync(
        CertificateAuthRequest request,
        string ipAddress,
        string userAgent)
    {
        try
        {
            _logger.LogInformation("Attempting certificate-based authentication from {IpAddress}", ipAddress);

            // Parse the certificate
            X509Certificate2? certificate = null;
            try
            {
                var certBytes = Convert.FromBase64String(request.CertificatePem.Replace("-----BEGIN CERTIFICATE-----", "")
                    .Replace("-----END CERTIFICATE-----", "")
                    .Replace("\n", "")
                    .Replace("\r", ""));
                certificate = new X509Certificate2(certBytes);
            }
            catch
            {
                _logger.LogWarning("Invalid certificate format provided");
                return new CertificateAuthResponse
                {
                    Success = false,
                    ErrorMessage = "Invalid certificate format"
                };
            }

            // Calculate thumbprint
            var thumbprint = certificate.Thumbprint;

            // Find enrolled certificate
            var userCert = await _context.Set<UserCertificate>()
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.Thumbprint == thumbprint && c.IsActive && !c.IsRevoked);

            if (userCert == null)
            {
                _logger.LogWarning("Certificate not found or not active: {Thumbprint}", thumbprint);
                return new CertificateAuthResponse
                {
                    Success = false,
                    ErrorMessage = "Certificate not enrolled or has been revoked"
                };
            }

            // Verify certificate is not expired
            if (certificate.NotAfter < DateTime.UtcNow)
            {
                _logger.LogWarning("Certificate has expired: {Thumbprint}", thumbprint);
                return new CertificateAuthResponse
                {
                    Success = false,
                    ErrorMessage = "Certificate has expired"
                };
            }

            if (certificate.NotBefore > DateTime.UtcNow)
            {
                _logger.LogWarning("Certificate is not yet valid: {Thumbprint}", thumbprint);
                return new CertificateAuthResponse
                {
                    Success = false,
                    ErrorMessage = "Certificate is not yet valid"
                };
            }

            // Check certificate chain validity
            var chainValid = VerifyCertificateChain(certificate);
            if (!chainValid)
            {
                _logger.LogWarning("Certificate chain validation failed: {Thumbprint}", thumbprint);
                return new CertificateAuthResponse
                {
                    Success = false,
                    ErrorMessage = "Certificate chain validation failed"
                };
            }

            // Check revocation status (CRL/OCSP)
            var revoked = await CheckRevocationStatusAsync(request.CertificatePem);
            if (revoked)
            {
                // Mark as revoked in our database
                userCert.IsRevoked = true;
                userCert.RevokedAt = DateTime.UtcNow;
                userCert.RevocationReason = "Certificate revoked by issuer";
                await _context.SaveChangesAsync();

                _logger.LogWarning("Certificate has been revoked: {Thumbprint}", thumbprint);
                return new CertificateAuthResponse
                {
                    Success = false,
                    ErrorMessage = "Certificate has been revoked"
                };
            }

            // Assess risk
            var riskAssessment = await _riskService.AssessRiskAsync(new RiskAssessmentRequest
            {
                UserId = userCert.UserId,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                DeviceFingerprint = request.DeviceFingerprint,
                AuthenticationMethod = "Certificate",
                MfaUsed = true // Certificate auth is considered strong
            });

            // Record risk assessment
            await _riskService.RecordAssessmentAsync(userCert.UserId, riskAssessment, "certificate_auth_success");

            // Update certificate usage
            userCert.LastUsedAt = DateTime.UtcNow;
            userCert.AuthenticationCount++;
            await _context.SaveChangesAsync();

            // Generate JWT tokens
            var accessToken = _jwtService.GenerateAccessToken(userCert.User, new List<string>());
            var refreshToken = GenerateRefreshToken();

            _logger.LogInformation("Certificate authentication successful for user {UserId}, cert {CertificateId}",
                userCert.UserId, userCert.Id);

            return new CertificateAuthResponse
            {
                Success = true,
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresAt = DateTime.UtcNow.AddMinutes(60),
                CertificateSubject = certificate.Subject,
                CertificateSerial = certificate.SerialNumber
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during certificate authentication");
            return new CertificateAuthResponse
            {
                Success = false,
                ErrorMessage = "An error occurred during authentication"
            };
        }
    }

    public async Task<EnrollCertificateResponse> EnrollCertificateAsync(EnrollCertificateRequest request)
    {
        try
        {
            _logger.LogInformation("Enrolling certificate for user {UserId}", request.UserId);

            // Verify user exists
            var user = await _context.Users.FindAsync(request.UserId);
            if (user == null)
            {
                throw new InvalidOperationException("User not found");
            }

            // Parse the certificate
            X509Certificate2 certificate;
            try
            {
                var certBytes = Convert.FromBase64String(request.CertificatePem.Replace("-----BEGIN CERTIFICATE-----", "")
                    .Replace("-----END CERTIFICATE-----", "")
                    .Replace("\n", "")
                    .Replace("\r", ""));
                certificate = new X509Certificate2(certBytes);
            }
            catch
            {
                throw new InvalidOperationException("Invalid certificate format");
            }

            // Extract certificate details
            var thumbprint = certificate.Thumbprint;
            var serialNumber = certificate.SerialNumber;
            var subject = certificate.Subject;
            var issuer = certificate.Issuer;
            var notBefore = certificate.NotBefore.ToUniversalTime();
            var notAfter = certificate.NotAfter.ToUniversalTime();

            // Check if certificate already enrolled
            var existing = await _context.Set<UserCertificate>()
                .FirstOrDefaultAsync(c => c.Thumbprint == thumbprint);

            if (existing != null)
            {
                throw new InvalidOperationException("Certificate already enrolled");
            }

            // Verify certificate is currently valid
            if (certificate.NotAfter < DateTime.UtcNow)
            {
                throw new InvalidOperationException("Certificate has expired");
            }

            if (certificate.NotBefore > DateTime.UtcNow)
            {
                throw new InvalidOperationException("Certificate is not yet valid");
            }

            // Extract public key
            var publicKeyPem = ExportPublicKeyToPem(certificate);

            // Create enrollment record
            var userCertificate = new UserCertificate
            {
                Id = Guid.NewGuid(),
                UserId = request.UserId,
                CertificateName = request.CertificateName,
                CertificateType = request.CertificateType,
                Thumbprint = thumbprint,
                SerialNumber = serialNumber,
                Subject = subject,
                Issuer = issuer,
                PublicKeyPem = publicKeyPem,
                NotBefore = notBefore,
                NotAfter = notAfter,
                IsActive = true,
                IsRevoked = false,
                AuthenticationCount = 0,
                EnrolledAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Set<UserCertificate>().Add(userCertificate);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Certificate enrolled successfully for user {UserId}, cert ID {CertificateId}",
                request.UserId, userCertificate.Id);

            return new EnrollCertificateResponse
            {
                CertificateId = userCertificate.Id,
                Thumbprint = thumbprint,
                Subject = subject,
                Issuer = issuer,
                NotAfter = notAfter,
                EnrolledAt = userCertificate.EnrolledAt
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enrolling certificate for user {UserId}", request.UserId);
            throw;
        }
    }

    public async Task<IEnumerable<UserCertificateDto>> GetUserCertificatesAsync(Guid userId)
    {
        try
        {
            var certificates = await _context.Set<UserCertificate>()
                .Where(c => c.UserId == userId)
                .OrderByDescending(c => c.EnrolledAt)
                .ToListAsync();

            return certificates.Select(c => new UserCertificateDto
            {
                Id = c.Id,
                UserId = c.UserId,
                CertificateName = c.CertificateName,
                CertificateType = c.CertificateType,
                Thumbprint = c.Thumbprint,
                Subject = c.Subject,
                Issuer = c.Issuer,
                NotBefore = c.NotBefore,
                NotAfter = c.NotAfter,
                IsActive = c.IsActive,
                IsRevoked = c.IsRevoked,
                LastUsedAt = c.LastUsedAt,
                EnrolledAt = c.EnrolledAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting certificates for user {UserId}", userId);
            return Enumerable.Empty<UserCertificateDto>();
        }
    }

    public async Task<bool> RevokeCertificateAsync(Guid certificateId, string reason)
    {
        try
        {
            var certificate = await _context.Set<UserCertificate>()
                .FirstOrDefaultAsync(c => c.Id == certificateId);

            if (certificate == null)
            {
                return false;
            }

            certificate.IsRevoked = true;
            certificate.IsActive = false;
            certificate.RevokedAt = DateTime.UtcNow;
            certificate.RevocationReason = reason;
            certificate.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Certificate {CertificateId} revoked. Reason: {Reason}", certificateId, reason);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking certificate {CertificateId}", certificateId);
            return false;
        }
    }

    public async Task<bool> VerifyCertificateAsync(string thumbprint)
    {
        try
        {
            var certificate = await _context.Set<UserCertificate>()
                .FirstOrDefaultAsync(c => c.Thumbprint == thumbprint);

            return certificate != null && certificate.IsActive && !certificate.IsRevoked
                && certificate.NotAfter > DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying certificate {Thumbprint}", thumbprint);
            return false;
        }
    }

    public async Task<bool> CheckRevocationStatusAsync(string certificatePem)
    {
        try
        {
            // Parse certificate
            var certBytes = Convert.FromBase64String(certificatePem.Replace("-----BEGIN CERTIFICATE-----", "")
                .Replace("-----END CERTIFICATE-----", "")
                .Replace("\n", "")
                .Replace("\r", ""));
            var certificate = new X509Certificate2(certBytes);

            // Build certificate chain
            using var chain = new X509Chain();
            chain.ChainPolicy.RevocationMode = X509RevocationMode.Online;
            chain.ChainPolicy.RevocationFlag = X509RevocationFlag.EntireChain;
            chain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;

            var chainBuilt = chain.Build(certificate);

            if (!chainBuilt)
            {
                // Check if revocation error
                foreach (var status in chain.ChainStatus)
                {
                    if (status.Status == X509ChainStatusFlags.Revoked)
                    {
                        _logger.LogWarning("Certificate is revoked: {Thumbprint}", certificate.Thumbprint);
                        return true;
                    }

                    if (status.Status == X509ChainStatusFlags.RevocationStatusUnknown ||
                        status.Status == X509ChainStatusFlags.OfflineRevocation)
                    {
                        // Cannot determine revocation status - proceed with caution
                        _logger.LogWarning("Cannot determine revocation status: {Thumbprint}, Status: {Status}",
                            certificate.Thumbprint, status.Status);
                        // For security, we could fail here, but for availability we'll allow
                        return false;
                    }
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking certificate revocation status");
            // If we can't check, fail open for availability (or fail closed for security)
            return false;
        }
    }

    #region Private Helper Methods

    private bool VerifyCertificateChain(X509Certificate2 certificate)
    {
        try
        {
            using var chain = new X509Chain();
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck; // We check this separately
            chain.ChainPolicy.VerificationFlags = X509VerificationFlags.IgnoreNotTimeValid;

            var chainBuilt = chain.Build(certificate);

            if (!chainBuilt)
            {
                foreach (var status in chain.ChainStatus)
                {
                    _logger.LogWarning("Certificate chain status: {Status}", status.Status);
                }
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying certificate chain");
            return false;
        }
    }

    private static string ExportPublicKeyToPem(X509Certificate2 certificate)
    {
        var publicKey = certificate.GetPublicKey();
        var publicKeyBase64 = Convert.ToBase64String(publicKey);
        return $"-----BEGIN PUBLIC KEY-----\n{publicKeyBase64}\n-----END PUBLIC KEY-----";
    }

    private static string GenerateRefreshToken()
    {
        var bytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }

    #endregion
}
