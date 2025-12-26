using USP.Core.Models.DTOs.Rotation;

namespace USP.Core.Services.Rotation;

/// <summary>
/// Service for automated cloud credential rotation (AWS, Azure, GCP)
/// </summary>
public interface ICloudCredentialRotationService
{
    /// <summary>
    /// Create cloud credential rotation configuration
    /// </summary>
    Task<CloudCredentialRotationDto> CreateRotationConfigAsync(Guid userId, CreateCloudCredentialRotationRequest request);

    /// <summary>
    /// Get cloud credential rotation configuration
    /// </summary>
    Task<CloudCredentialRotationDto?> GetRotationConfigAsync(Guid rotationId, Guid userId);

    /// <summary>
    /// Get all cloud credential rotation configurations
    /// </summary>
    Task<List<CloudCredentialRotationDto>> GetRotationConfigsAsync(Guid userId, string? provider = null);

    /// <summary>
    /// Delete cloud credential rotation configuration
    /// </summary>
    Task<bool> DeleteRotationConfigAsync(Guid rotationId, Guid userId);

    /// <summary>
    /// Manually trigger cloud credential rotation
    /// </summary>
    Task<CloudCredentialRotationResultDto> RotateCredentialAsync(Guid rotationId, Guid userId);

    /// <summary>
    /// Process scheduled cloud credential rotations (background job)
    /// </summary>
    Task<int> ProcessScheduledRotationsAsync();

    /// <summary>
    /// Rotate AWS IAM access key
    /// </summary>
    Task<CloudCredentialRotationResultDto> RotateAwsAccessKeyAsync(Guid rotationId, Guid userId);

    /// <summary>
    /// Rotate Azure service principal secret
    /// </summary>
    Task<CloudCredentialRotationResultDto> RotateAzureServicePrincipalAsync(Guid rotationId, Guid userId);

    /// <summary>
    /// Rotate GCP service account key
    /// </summary>
    Task<CloudCredentialRotationResultDto> RotateGcpServiceAccountKeyAsync(Guid rotationId, Guid userId);

    /// <summary>
    /// Validate cloud credentials
    /// </summary>
    Task<bool> ValidateCredentialsAsync(Guid rotationId, Guid userId);

    /// <summary>
    /// Get credentials due for rotation
    /// </summary>
    Task<List<CloudCredentialRotationDto>> GetCredentialsDueForRotationAsync(Guid userId);
}
