namespace USP.Core.Interfaces.Services.Authentication;

/// <summary>
/// Service for backup codes generation and verification
/// </summary>
public interface IBackupCodesService
{
    /// <summary>
    /// Generate a set of backup codes
    /// </summary>
    List<string> GenerateBackupCodes(int count = 10);

    /// <summary>
    /// Hash a backup code for storage
    /// </summary>
    string HashBackupCode(string code);

    /// <summary>
    /// Verify a backup code against its hash
    /// </summary>
    bool VerifyBackupCode(string code, string hash);
}
