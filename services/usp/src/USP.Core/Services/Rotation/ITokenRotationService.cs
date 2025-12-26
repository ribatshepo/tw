using USP.Core.Models.DTOs.Rotation;

namespace USP.Core.Services.Rotation;

/// <summary>
/// Service for automated token rotation (JWT, OAuth, PAT, Session)
/// </summary>
public interface ITokenRotationService
{
    /// <summary>
    /// Create token rotation configuration
    /// </summary>
    Task<TokenRotationDto> CreateRotationConfigAsync(Guid userId, CreateTokenRotationRequest request);

    /// <summary>
    /// Get token rotation configuration
    /// </summary>
    Task<TokenRotationDto?> GetRotationConfigAsync(Guid rotationId, Guid userId);

    /// <summary>
    /// Get all token rotation configurations
    /// </summary>
    Task<List<TokenRotationDto>> GetRotationConfigsAsync(Guid userId, string? tokenType = null);

    /// <summary>
    /// Delete token rotation configuration
    /// </summary>
    Task<bool> DeleteRotationConfigAsync(Guid rotationId, Guid userId);

    /// <summary>
    /// Manually trigger token rotation
    /// </summary>
    Task<TokenRotationResultDto> RotateTokenAsync(Guid rotationId, Guid userId);

    /// <summary>
    /// Process scheduled token rotations (background job)
    /// </summary>
    Task<int> ProcessScheduledRotationsAsync();

    /// <summary>
    /// Rotate JWT refresh token
    /// </summary>
    Task<TokenRotationResultDto> RotateJwtRefreshTokenAsync(Guid rotationId, Guid userId);

    /// <summary>
    /// Rotate OAuth access token
    /// </summary>
    Task<TokenRotationResultDto> RotateOAuthTokenAsync(Guid rotationId, Guid userId);

    /// <summary>
    /// Rotate personal access token
    /// </summary>
    Task<TokenRotationResultDto> RotatePersonalAccessTokenAsync(Guid rotationId, Guid userId);

    /// <summary>
    /// Rotate session token
    /// </summary>
    Task<TokenRotationResultDto> RotateSessionTokenAsync(Guid rotationId, Guid userId);
}
