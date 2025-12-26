using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using USP.Core.Services.PAM;

namespace USP.Infrastructure.Services.PAM.Connectors;

/// <summary>
/// SQL Server privileged account management connector
/// </summary>
public class SqlServerConnector : BaseConnector
{
    private readonly ILogger<SqlServerConnector> _logger;

    public override string Platform => "SQLServer";

    public SqlServerConnector(ILogger<SqlServerConnector> logger)
    {
        _logger = logger;
    }

    public override async Task<PasswordRotationResult> RotatePasswordAsync(
        string hostAddress,
        int? port,
        string username,
        string currentPassword,
        string newPassword,
        string? databaseName = null,
        string? connectionDetails = null)
    {
        var result = new PasswordRotationResult
        {
            Success = false,
            RotatedAt = DateTime.UtcNow
        };

        try
        {
            // Build connection string with current credentials
            var connectionString = BuildConnectionString(
                hostAddress,
                port ?? 1433,
                username,
                currentPassword,
                databaseName ?? "master");

            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            // Escape single quotes in password
            var escapedNewPassword = newPassword.Replace("'", "''");

            // Execute ALTER LOGIN command to change password
            var sql = $"ALTER LOGIN [{username}] WITH PASSWORD = '{escapedNewPassword}';";

            await using var command = new SqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync();

            result.Success = true;
            result.Details = $"Password rotated successfully for SQL Server login {username} on {hostAddress}";

            _logger.LogInformation(
                "Password rotated successfully for SQL Server login {Username} on {Host}",
                username,
                hostAddress);
        }
        catch (SqlException ex) when (ex.Number == 18456)
        {
            result.ErrorMessage = "Authentication failed with current password";
            result.Details = $"Login failed: {ex.Message}";

            _logger.LogError(ex,
                "Authentication failed for SQL Server login {Username} on {Host}",
                username,
                hostAddress);
        }
        catch (SqlException ex) when (ex.Number == 15118)
        {
            result.ErrorMessage = "Password does not meet complexity requirements";
            result.Details = $"Password policy violation: {ex.Message}";

            _logger.LogError(ex,
                "Password policy violation for SQL Server login {Username} on {Host}",
                username,
                hostAddress);
        }
        catch (SqlException ex)
        {
            result.ErrorMessage = "SQL Server error occurred";
            result.Details = $"SQL Error {ex.Number}: {ex.Message}";

            _logger.LogError(ex,
                "SQL Server error rotating password for login {Username} on {Host}",
                username,
                hostAddress);
        }
        catch (Exception ex)
        {
            result.ErrorMessage = ex.Message;
            result.Details = $"Failed to rotate password: {ex.Message}";

            _logger.LogError(ex,
                "Unexpected error rotating password for SQL Server login {Username} on {Host}",
                username,
                hostAddress);
        }

        return result;
    }

    public override async Task<bool> VerifyCredentialsAsync(
        string hostAddress,
        int? port,
        string username,
        string password,
        string? databaseName = null,
        string? connectionDetails = null)
    {
        try
        {
            var connectionString = BuildConnectionString(
                hostAddress,
                port ?? 1433,
                username,
                password,
                databaseName ?? "master");

            // Set connection timeout to 10 seconds for verification
            var builder = new SqlConnectionStringBuilder(connectionString)
            {
                ConnectTimeout = 10
            };

            await using var connection = new SqlConnection(builder.ConnectionString);
            await connection.OpenAsync();

            // Execute a simple query to verify connection
            await using var command = new SqlCommand("SELECT 1;", connection);
            await command.ExecuteScalarAsync();

            _logger.LogDebug(
                "Credentials verified successfully for SQL Server login {Username} on {Host}",
                username,
                hostAddress);

            return true;
        }
        catch (SqlException ex)
        {
            _logger.LogWarning(ex,
                "Failed to verify credentials for SQL Server login {Username} on {Host}",
                username,
                hostAddress);

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Error verifying credentials for SQL Server login {Username} on {Host}",
                username,
                hostAddress);

            return false;
        }
    }

    /// <summary>
    /// Discover privileged logins on SQL Server
    /// </summary>
    public async Task<List<string>> DiscoverPrivilegedLoginsAsync(
        string hostAddress,
        int? port,
        string adminUsername,
        string adminPassword,
        string? databaseName = null)
    {
        var privilegedLogins = new List<string>();

        try
        {
            var connectionString = BuildConnectionString(
                hostAddress,
                port ?? 1433,
                adminUsername,
                adminPassword,
                "master");

            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            // Query for logins with sysadmin or other privileged roles
            var sql = @"
                SELECT DISTINCT l.name
                FROM sys.server_principals l
                LEFT JOIN sys.server_role_members rm ON l.principal_id = rm.member_principal_id
                LEFT JOIN sys.server_principals r ON rm.role_principal_id = r.principal_id
                WHERE l.type IN ('S', 'U', 'G')  -- SQL login, Windows user, Windows group
                  AND l.is_disabled = 0
                  AND (
                    r.name IN ('sysadmin', 'securityadmin', 'serveradmin', 'setupadmin', 'processadmin', 'diskadmin', 'dbcreator', 'bulkadmin')
                    OR l.name = 'sa'
                  )
                ORDER BY l.name;";

            await using var command = new SqlCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                privilegedLogins.Add(reader.GetString(0));
            }

            _logger.LogInformation(
                "Discovered {Count} privileged logins on SQL Server {Host}",
                privilegedLogins.Count,
                hostAddress);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to discover privileged logins on SQL Server {Host}",
                hostAddress);
        }

        return privilegedLogins;
    }

    /// <summary>
    /// Get login roles and permissions
    /// </summary>
    public async Task<Dictionary<string, List<string>>> GetLoginRolesAsync(
        string hostAddress,
        int? port,
        string adminUsername,
        string adminPassword,
        string loginName)
    {
        var roles = new Dictionary<string, List<string>>
        {
            { "ServerRoles", new List<string>() },
            { "DatabaseRoles", new List<string>() },
            { "Permissions", new List<string>() }
        };

        try
        {
            var connectionString = BuildConnectionString(
                hostAddress,
                port ?? 1433,
                adminUsername,
                adminPassword,
                "master");

            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            // Get server roles
            var serverRolesSql = @"
                SELECT r.name
                FROM sys.server_principals l
                JOIN sys.server_role_members rm ON l.principal_id = rm.member_principal_id
                JOIN sys.server_principals r ON rm.role_principal_id = r.principal_id
                WHERE l.name = @LoginName;";

            await using var command = new SqlCommand(serverRolesSql, connection);
            command.Parameters.AddWithValue("@LoginName", loginName);

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                roles["ServerRoles"].Add(reader.GetString(0));
            }

            _logger.LogDebug(
                "Retrieved roles for SQL Server login {LoginName} on {Host}",
                loginName,
                hostAddress);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to get roles for SQL Server login {LoginName} on {Host}",
                loginName,
                hostAddress);
        }

        return roles;
    }

    private static string BuildConnectionString(
        string hostAddress,
        int port,
        string username,
        string password,
        string database)
    {
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = port == 1433 ? hostAddress : $"{hostAddress},{port}",
            InitialCatalog = database,
            UserID = username,
            Password = password,
            TrustServerCertificate = true, // For dev/testing - should be false in production with proper cert validation
            Encrypt = true,
            ConnectTimeout = 30
        };

        return builder.ConnectionString;
    }
}
