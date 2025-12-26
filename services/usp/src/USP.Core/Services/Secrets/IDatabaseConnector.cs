namespace USP.Core.Services.Secrets;

/// <summary>
/// Interface for database-specific connectors
/// </summary>
public interface IDatabaseConnector
{
    /// <summary>
    /// Database plugin name (postgresql, mysql, sqlserver, mongodb, redis, oracle, cassandra, elasticsearch)
    /// </summary>
    string PluginName { get; }

    /// <summary>
    /// Verify database connection
    /// </summary>
    Task<bool> VerifyConnectionAsync(string connectionUrl, string? username, string? password);

    /// <summary>
    /// Create dynamic user with specified statements
    /// </summary>
    Task<(string username, string password)> CreateDynamicUserAsync(
        string connectionUrl,
        string adminUsername,
        string adminPassword,
        string creationStatements,
        int ttlSeconds);

    /// <summary>
    /// Revoke dynamic user
    /// </summary>
    Task<bool> RevokeDynamicUserAsync(
        string connectionUrl,
        string adminUsername,
        string adminPassword,
        string username,
        string? revocationStatements);

    /// <summary>
    /// Rotate root/admin credentials
    /// </summary>
    Task<string> RotateRootCredentialsAsync(
        string connectionUrl,
        string currentUsername,
        string currentPassword,
        string newPassword);

    /// <summary>
    /// Renew dynamic user (extend expiration if supported)
    /// </summary>
    Task<bool> RenewDynamicUserAsync(
        string connectionUrl,
        string adminUsername,
        string adminPassword,
        string username,
        string? renewStatements,
        int additionalTtlSeconds);

    /// <summary>
    /// Generate random username for dynamic credentials
    /// </summary>
    string GenerateUsername(string roleName);

    /// <summary>
    /// Generate secure random password
    /// </summary>
    string GeneratePassword(int length = 32);
}

/// <summary>
/// Result of credential creation
/// </summary>
public class DatabaseCredentialResult
{
    public bool Success { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
}
