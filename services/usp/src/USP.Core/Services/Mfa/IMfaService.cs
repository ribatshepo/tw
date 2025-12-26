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

    /// <summary>
    /// Send phone number verification code
    /// </summary>
    Task<bool> SendPhoneVerificationAsync(Guid userId, string phoneNumber);

    /// <summary>
    /// Verify phone number with code
    /// </summary>
    Task<bool> VerifyPhoneNumberAsync(Guid userId, string code);

    /// <summary>
    /// Enroll SMS MFA after phone verification
    /// </summary>
    Task<bool> EnrollSmsMfaAsync(Guid userId);

    /// <summary>
    /// Send SMS OTP code for authentication
    /// </summary>
    Task<bool> SendSmsOtpAsync(Guid userId);

    /// <summary>
    /// Verify SMS OTP code
    /// </summary>
    Task<bool> VerifySmsOtpAsync(Guid userId, string code);

    /// <summary>
    /// Send Voice OTP code for authentication
    /// </summary>
    Task<bool> SendVoiceOtpAsync(Guid userId);

    /// <summary>
    /// Send push notification for MFA approval
    /// </summary>
    Task<bool> SendPushNotificationAsync(Guid userId, string message, string actionType = "approve");

    /// <summary>
    /// Verify push notification approval
    /// </summary>
    Task<bool> VerifyPushApprovalAsync(Guid userId, bool approved);

    /// <summary>
    /// Enroll push notification MFA
    /// </summary>
    Task<bool> EnrollPushNotificationAsync(Guid userId, string deviceToken, string devicePlatform);

    /// <summary>
    /// Enroll hardware token (e.g., YubiKey) for MFA
    /// </summary>
    Task<bool> EnrollHardwareTokenAsync(Guid userId, string tokenSerial, string tokenType = "YubiKey");

    /// <summary>
    /// Verify hardware token OTP
    /// </summary>
    Task<bool> VerifyHardwareTokenAsync(Guid userId, string otp);
}
