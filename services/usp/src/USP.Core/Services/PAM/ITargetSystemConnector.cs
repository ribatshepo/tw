namespace USP.Core.Services.PAM;

/// <summary>
/// Interface for target system connectors to rotate passwords
/// </summary>
public interface ITargetSystemConnector
{
    /// <summary>
    /// Platform name (e.g., "PostgreSQL", "MySQL", "Windows", "Linux", "AWS", "Azure")
    /// </summary>
    string Platform { get; }

    /// <summary>
    /// Generate a strong password for this platform
    /// </summary>
    string GeneratePassword(int length = 32);

    /// <summary>
    /// Rotate password on the target system
    /// </summary>
    /// <param name="hostAddress">Target host address</param>
    /// <param name="port">Target port</param>
    /// <param name="username">Username to rotate</param>
    /// <param name="currentPassword">Current password for authentication</param>
    /// <param name="newPassword">New password to set</param>
    /// <param name="databaseName">Database name (if applicable)</param>
    /// <param name="connectionDetails">Additional connection details (JSON)</param>
    Task<PasswordRotationResult> RotatePasswordAsync(
        string hostAddress,
        int? port,
        string username,
        string currentPassword,
        string newPassword,
        string? databaseName = null,
        string? connectionDetails = null);

    /// <summary>
    /// Verify credentials work on the target system
    /// </summary>
    Task<bool> VerifyCredentialsAsync(
        string hostAddress,
        int? port,
        string username,
        string password,
        string? databaseName = null,
        string? connectionDetails = null);
}

/// <summary>
/// Result of password rotation operation
/// </summary>
public class PasswordRotationResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime RotatedAt { get; set; }
    public string? Details { get; set; }
}
