using USP.Core.Models.DTOs.Mfa;
using USP.Core.Models.Entities;

namespace USP.Core.Services.Mfa;

/// <summary>
/// Service for Multi-Factor Authentication operations
/// </summary>
public interface IMfaService
{
    /// <summary>
    /// Enroll user in TOTP (Time-based One-Time Password)
    /// </summary>
    Task<EnrollTotpResponse> EnrollTotpAsync(Guid userId, string deviceName);

    /// <summary>
    /// Verify TOTP code and complete enrollment
    /// </summary>
    Task<VerifyTotpResponse> VerifyAndCompleteTotpEnrollmentAsync(Guid userId, string code);

    /// <summary>
    /// Verify TOTP code for authentication
    /// </summary>
    Task<bool> VerifyTotpCodeAsync(Guid userId, string code);

    /// <summary>
    /// Generate backup codes for user
    /// </summary>
    Task<GenerateBackupCodesResponse> GenerateBackupCodesAsync(Guid userId);

    /// <summary>
    /// Verify backup code
    /// </summary>
    Task<bool> VerifyBackupCodeAsync(Guid userId, string code);

    /// <summary>
    /// Get user's MFA devices
    /// </summary>
    Task<IEnumerable<MfaDeviceDto>> GetUserMfaDevicesAsync(Guid userId);

    /// <summary>
    /// Disable MFA for user
    /// </summary>
    Task<bool> DisableMfaAsync(Guid userId, string password);

    /// <summary>
    /// Remove MFA device
    /// </summary>
    Task<bool> RemoveMfaDeviceAsync(Guid userId, Guid deviceId);

    /// <summary>
    /// Set primary MFA device
    /// </summary>
    Task<bool> SetPrimaryMfaDeviceAsync(Guid userId, Guid deviceId);
}
