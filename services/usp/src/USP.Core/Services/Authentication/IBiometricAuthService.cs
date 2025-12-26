using USP.Core.Models.DTOs.Authentication;

namespace USP.Core.Services.Authentication;

/// <summary>
/// Service for biometric authentication (fingerprint, face, iris, voice)
/// </summary>
public interface IBiometricAuthService
{
    /// <summary>
    /// Enroll a new biometric template for a user
    /// </summary>
    Task<EnrollBiometricResponse> EnrollBiometricAsync(EnrollBiometricRequest request);

    /// <summary>
    /// Authenticate user using biometric template
    /// </summary>
    Task<BiometricAuthResponse> AuthenticateWithBiometricAsync(BiometricAuthRequest request, string ipAddress, string userAgent);

    /// <summary>
    /// Authenticate with biometric or fallback to PIN
    /// </summary>
    Task<BiometricAuthResponse> AuthenticateWithBiometricOrPinAsync(BiometricPinAuthRequest request, string ipAddress, string userAgent);

    /// <summary>
    /// Get all enrolled biometrics for a user
    /// </summary>
    Task<IEnumerable<BiometricDeviceDto>> GetUserBiometricsAsync(Guid userId);

    /// <summary>
    /// Remove/deactivate a biometric template
    /// </summary>
    Task<bool> RemoveBiometricAsync(Guid biometricId);

    /// <summary>
    /// Verify biometric template matches stored template
    /// </summary>
    Task<(bool isMatch, int confidenceScore)> VerifyBiometricAsync(Guid userId, string biometricType, string templateData);

    /// <summary>
    /// Set biometric as primary authentication method
    /// </summary>
    Task<bool> SetPrimaryBiometricAsync(Guid userId, Guid biometricId);
}
