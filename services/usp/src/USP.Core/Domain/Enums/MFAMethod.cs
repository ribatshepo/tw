namespace USP.Core.Domain.Enums;

/// <summary>
/// Represents the multi-factor authentication method types
/// </summary>
public enum MFAMethod
{
    /// <summary>
    /// Time-based One-Time Password (RFC 6238)
    /// </summary>
    TOTP = 0,

    /// <summary>
    /// Email-based OTP (6-digit code sent via email)
    /// </summary>
    Email = 1,

    /// <summary>
    /// SMS-based OTP (6-digit code sent via SMS)
    /// </summary>
    SMS = 2,

    /// <summary>
    /// Push notification approval (mobile app)
    /// </summary>
    Push = 3,

    /// <summary>
    /// WebAuthn/FIDO2 authentication (security keys, biometrics)
    /// </summary>
    WebAuthn = 4,

    /// <summary>
    /// Backup codes (one-time use recovery codes)
    /// </summary>
    BackupCode = 5
}
