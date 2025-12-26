using USP.Core.Models.DTOs.Rotation;

namespace USP.Core.Services.Rotation;

/// <summary>
/// Service for automated certificate rotation with ACME protocol support
/// </summary>
public interface ICertificateRotationService
{
    /// <summary>
    /// Create certificate rotation configuration
    /// </summary>
    Task<CertificateRotationDto> CreateRotationConfigAsync(Guid userId, CreateCertificateRotationRequest request);

    /// <summary>
    /// Update certificate rotation configuration
    /// </summary>
    Task<bool> UpdateRotationConfigAsync(Guid rotationId, Guid userId, UpdateCertificateRotationRequest request);

    /// <summary>
    /// Get certificate rotation configuration by ID
    /// </summary>
    Task<CertificateRotationDto?> GetRotationConfigAsync(Guid rotationId, Guid userId);

    /// <summary>
    /// Get all certificate rotation configurations for user
    /// </summary>
    Task<List<CertificateRotationDto>> GetRotationConfigsAsync(Guid userId, bool? activeOnly = null);

    /// <summary>
    /// Delete certificate rotation configuration
    /// </summary>
    Task<bool> DeleteRotationConfigAsync(Guid rotationId, Guid userId);

    /// <summary>
    /// Manually trigger certificate rotation
    /// </summary>
    Task<CertificateRotationResultDto> RotateCertificateAsync(Guid rotationId, Guid userId);

    /// <summary>
    /// Process scheduled certificate rotations (background job)
    /// </summary>
    Task<int> ProcessScheduledRotationsAsync();

    /// <summary>
    /// Process expiration alerts (background job)
    /// </summary>
    Task<int> ProcessExpirationAlertsAsync();

    /// <summary>
    /// Get certificates due for rotation
    /// </summary>
    Task<List<CertificateRotationDto>> GetCertificatesDueForRotationAsync(Guid userId);

    /// <summary>
    /// Get certificates expiring soon
    /// </summary>
    Task<List<CertificateExpirationAlertDto>> GetExpiringCertificatesAsync(Guid userId, int daysThreshold = 30);

    /// <summary>
    /// Get rotation history for a certificate
    /// </summary>
    Task<List<CertificateRotationHistoryDto>> GetRotationHistoryAsync(Guid rotationId, Guid userId, int? limit = 50);

    /// <summary>
    /// Validate certificate chain
    /// </summary>
    Task<(bool IsValid, string? ErrorMessage)> ValidateCertificateChainAsync(Guid rotationId, Guid userId);

    /// <summary>
    /// Renew certificate via ACME protocol (Let's Encrypt)
    /// </summary>
    Task<CertificateRotationResultDto> RenewAcmeCertificateAsync(Guid rotationId, Guid userId);

    /// <summary>
    /// Renew certificate via Private CA
    /// </summary>
    Task<CertificateRotationResultDto> RenewPrivateCaCertificateAsync(Guid rotationId, Guid userId);

    /// <summary>
    /// Deploy certificate to target systems
    /// </summary>
    Task<Dictionary<string, bool>> DeployCertificateAsync(Guid rotationId, Guid userId, string certificateData);

    /// <summary>
    /// Rollback certificate rotation
    /// </summary>
    Task<bool> RollbackRotationAsync(Guid rotationHistoryId, Guid userId);

    /// <summary>
    /// Get certificate rotation statistics
    /// </summary>
    Task<CertificateRotationStatisticsDto> GetStatisticsAsync(Guid userId);

    /// <summary>
    /// Test ACME account connectivity
    /// </summary>
    Task<bool> TestAcmeAccountAsync(string acmeAccountUrl);

    /// <summary>
    /// Test deployment target connectivity
    /// </summary>
    Task<bool> TestDeploymentTargetAsync(string targetUrl);
}
