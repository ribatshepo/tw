namespace USP.Core.Interfaces.Services.Authentication;

/// <summary>
/// Service for TOTP (Time-based One-Time Password) generation and verification
/// </summary>
public interface ITOTPService
{
    /// <summary>
    /// Generate a new TOTP secret for enrollment
    /// </summary>
    string GenerateSecret();

    /// <summary>
    /// Generate provisioning URI for QR code (otpauth://)
    /// </summary>
    string GenerateProvisioningUri(string email, string secret, string issuer = "USP Security Platform");

    /// <summary>
    /// Verify a TOTP code against a secret
    /// </summary>
    bool VerifyCode(string secret, string code, int toleranceSteps = 1);

    /// <summary>
    /// Generate current TOTP code (for testing)
    /// </summary>
    string GenerateCode(string secret);
}
