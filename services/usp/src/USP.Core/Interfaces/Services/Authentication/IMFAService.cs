using USP.Core.Domain.Entities.Identity;
using USP.Core.Domain.Enums;

namespace USP.Core.Interfaces.Services.Authentication;

/// <summary>
/// Main MFA orchestration service for enrollment, verification, and management
/// </summary>
public interface IMFAService
{
    /// <summary>
    /// Enroll a new MFA device for a user
    /// </summary>
    Task<MFADevice> EnrollDeviceAsync(
        string userId,
        MFAMethod method,
        string deviceName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Verify MFA code during enrollment
    /// </summary>
    Task<bool> VerifyEnrollmentAsync(
        string userId,
        string deviceId,
        string code,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Verify MFA code during authentication
    /// </summary>
    Task<bool> VerifyMFACodeAsync(
        string userId,
        MFAMethod method,
        string code,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all MFA devices for a user
    /// </summary>
    Task<List<MFADevice>> GetUserMFADevicesAsync(
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Remove an MFA device
    /// </summary>
    Task RemoveMFADeviceAsync(
        string userId,
        string deviceId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Set a device as primary MFA method
    /// </summary>
    Task SetPrimaryDeviceAsync(
        string userId,
        string deviceId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate backup codes for a user
    /// </summary>
    Task<List<string>> GenerateBackupCodesAsync(
        string userId,
        int count = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Trust a device to skip MFA for a period
    /// </summary>
    Task<TrustedDevice> TrustDeviceAsync(
        string userId,
        string deviceFingerprint,
        string deviceName,
        string ipAddress,
        string userAgent,
        int trustDays = 30,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if a device is trusted
    /// </summary>
    Task<bool> IsDeviceTrustedAsync(
        string userId,
        string deviceFingerprint,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all trusted devices for a user
    /// </summary>
    Task<List<TrustedDevice>> GetTrustedDevicesAsync(
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Revoke trust for a device
    /// </summary>
    Task RevokeTrustedDeviceAsync(
        string userId,
        string deviceId,
        CancellationToken cancellationToken = default);
}
