using Microsoft.Extensions.Logging;
using Oracle.ManagedDataAccess.Client;
using USP.Core.Services.PAM;

namespace USP.Infrastructure.Services.PAM.Connectors;

/// <summary>
/// Oracle Database privileged account management connector
/// </summary>
public class OracleConnector : BaseConnector
{
    private readonly ILogger<OracleConnector> _logger;

    public override string Platform => "Oracle";

    public OracleConnector(ILogger<OracleConnector> logger)
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
                port ?? 1521,
                username,
                currentPassword,
                databaseName ?? "ORCL");

            await using var connection = new OracleConnection(connectionString);
            await connection.OpenAsync();

            // Escape single quotes in password
            var escapedNewPassword = newPassword.Replace("'", "''");

            // Execute ALTER USER command to change password
            var sql = $"ALTER USER \"{username}\" IDENTIFIED BY \"{escapedNewPassword}\"";

            await using var command = new OracleCommand(sql, connection);
            await command.ExecuteNonQueryAsync();

            result.Success = true;
            result.Details = $"Password rotated successfully for Oracle user {username} on {hostAddress}";

            _logger.LogInformation(
                "Password rotated successfully for Oracle user {Username} on {Host}",
                username,
                hostAddress);
        }
        catch (OracleException ex) when (ex.Number == 1017)
        {
            result.ErrorMessage = "Authentication failed with current password";
            result.Details = $"Invalid username/password: {ex.Message}";

            _logger.LogError(ex,
                "Authentication failed for Oracle user {Username} on {Host}",
                username,
                hostAddress);
        }
        catch (OracleException ex) when (ex.Number == 988)
        {
            result.ErrorMessage = "Password does not meet complexity requirements";
            result.Details = $"Password policy violation: {ex.Message}";

            _logger.LogError(ex,
                "Password policy violation for Oracle user {Username} on {Host}",
                username,
                hostAddress);
        }
        catch (OracleException ex)
        {
            result.ErrorMessage = "Oracle error occurred";
            result.Details = $"Oracle Error {ex.Number}: {ex.Message}";

            _logger.LogError(ex,
                "Oracle error rotating password for user {Username} on {Host}",
                username,
                hostAddress);
        }
        catch (Exception ex)
        {
            result.ErrorMessage = ex.Message;
            result.Details = $"Failed to rotate password: {ex.Message}";

            _logger.LogError(ex,
                "Unexpected error rotating password for Oracle user {Username} on {Host}",
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
                port ?? 1521,
                username,
                password,
                databaseName ?? "ORCL");

            // Add connection timeout
            var builder = new OracleConnectionStringBuilder(connectionString)
            {
                ConnectionTimeout = 10
            };

            await using var connection = new OracleConnection(builder.ConnectionString);
            await connection.OpenAsync();

            // Execute a simple query to verify connection
            await using var command = new OracleCommand("SELECT 1 FROM DUAL", connection);
            await command.ExecuteScalarAsync();

            _logger.LogDebug(
                "Credentials verified successfully for Oracle user {Username} on {Host}",
                username,
                hostAddress);

            return true;
        }
        catch (OracleException ex)
        {
            _logger.LogWarning(ex,
                "Failed to verify credentials for Oracle user {Username} on {Host}",
                username,
                hostAddress);

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Error verifying credentials for Oracle user {Username} on {Host}",
                username,
                hostAddress);

            return false;
        }
    }

    /// <summary>
    /// Discover privileged users on Oracle database
    /// </summary>
    public async Task<List<string>> DiscoverPrivilegedUsersAsync(
        string hostAddress,
        int? port,
        string adminUsername,
        string adminPassword,
        string? databaseName = null)
    {
        var privilegedUsers = new List<string>();

        try
        {
            var connectionString = BuildConnectionString(
                hostAddress,
                port ?? 1521,
                adminUsername,
                adminPassword,
                databaseName ?? "ORCL");

            await using var connection = new OracleConnection(connectionString);
            await connection.OpenAsync();

            // Query for users with DBA or other privileged roles
            var sql = @"
                SELECT DISTINCT u.username
                FROM dba_users u
                LEFT JOIN dba_role_privs rp ON u.username = rp.grantee
                WHERE u.account_status = 'OPEN'
                  AND (
                    rp.granted_role IN ('DBA', 'SYSDBA', 'SYSOPER', 'CONNECT', 'RESOURCE')
                    OR u.username IN ('SYS', 'SYSTEM')
                  )
                ORDER BY u.username";

            await using var command = new OracleCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                privilegedUsers.Add(reader.GetString(0));
            }

            _logger.LogInformation(
                "Discovered {Count} privileged users on Oracle database {Host}",
                privilegedUsers.Count,
                hostAddress);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to discover privileged users on Oracle database {Host}",
                hostAddress);
        }

        return privilegedUsers;
    }

    /// <summary>
    /// Get user roles and privileges
    /// </summary>
    public async Task<Dictionary<string, List<string>>> GetUserPrivilegesAsync(
        string hostAddress,
        int? port,
        string adminUsername,
        string adminPassword,
        string userName,
        string? databaseName = null)
    {
        var privileges = new Dictionary<string, List<string>>
        {
            { "Roles", new List<string>() },
            { "SystemPrivileges", new List<string>() },
            { "ObjectPrivileges", new List<string>() }
        };

        try
        {
            var connectionString = BuildConnectionString(
                hostAddress,
                port ?? 1521,
                adminUsername,
                adminPassword,
                databaseName ?? "ORCL");

            await using var connection = new OracleConnection(connectionString);
            await connection.OpenAsync();

            // Get roles
            var rolesSql = @"
                SELECT granted_role
                FROM dba_role_privs
                WHERE grantee = :username
                ORDER BY granted_role";

            await using var rolesCommand = new OracleCommand(rolesSql, connection);
            rolesCommand.Parameters.Add(new OracleParameter("username", userName.ToUpper()));

            await using var rolesReader = await rolesCommand.ExecuteReaderAsync();
            while (await rolesReader.ReadAsync())
            {
                privileges["Roles"].Add(rolesReader.GetString(0));
            }

            // Get system privileges
            var sysPrvSql = @"
                SELECT privilege
                FROM dba_sys_privs
                WHERE grantee = :username
                ORDER BY privilege";

            await using var sysCommand = new OracleCommand(sysPrvSql, connection);
            sysCommand.Parameters.Add(new OracleParameter("username", userName.ToUpper()));

            await using var sysReader = await sysCommand.ExecuteReaderAsync();
            while (await sysReader.ReadAsync())
            {
                privileges["SystemPrivileges"].Add(sysReader.GetString(0));
            }

            // Get object privileges (limited to first 100)
            var objPrvSql = @"
                SELECT owner || '.' || table_name || ' (' || privilege || ')' as obj_priv
                FROM dba_tab_privs
                WHERE grantee = :username
                  AND ROWNUM <= 100
                ORDER BY owner, table_name";

            await using var objCommand = new OracleCommand(objPrvSql, connection);
            objCommand.Parameters.Add(new OracleParameter("username", userName.ToUpper()));

            await using var objReader = await objCommand.ExecuteReaderAsync();
            while (await objReader.ReadAsync())
            {
                privileges["ObjectPrivileges"].Add(objReader.GetString(0));
            }

            _logger.LogDebug(
                "Retrieved privileges for Oracle user {Username} on {Host}",
                userName,
                hostAddress);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to get privileges for Oracle user {Username} on {Host}",
                userName,
                hostAddress);
        }

        return privileges;
    }

    /// <summary>
    /// Check password expiration status
    /// </summary>
    public async Task<DateTime?> GetPasswordExpiryAsync(
        string hostAddress,
        int? port,
        string adminUsername,
        string adminPassword,
        string userName,
        string? databaseName = null)
    {
        try
        {
            var connectionString = BuildConnectionString(
                hostAddress,
                port ?? 1521,
                adminUsername,
                adminPassword,
                databaseName ?? "ORCL");

            await using var connection = new OracleConnection(connectionString);
            await connection.OpenAsync();

            var sql = @"
                SELECT expiry_date
                FROM dba_users
                WHERE username = :username";

            await using var command = new OracleCommand(sql, connection);
            command.Parameters.Add(new OracleParameter("username", userName.ToUpper()));

            var result = await command.ExecuteScalarAsync();

            if (result != null && result != DBNull.Value)
            {
                return Convert.ToDateTime(result);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to get password expiry for Oracle user {Username} on {Host}",
                userName,
                hostAddress);
        }

        return null;
    }

    private static string BuildConnectionString(
        string hostAddress,
        int port,
        string username,
        string password,
        string serviceName)
    {
        var builder = new OracleConnectionStringBuilder
        {
            DataSource = $"(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST={hostAddress})(PORT={port}))(CONNECT_DATA=(SERVICE_NAME={serviceName})))",
            UserID = username,
            Password = password,
            ConnectionTimeout = 30,
            Pooling = false // Disable pooling for rotation operations
        };

        return builder.ConnectionString;
    }
}
