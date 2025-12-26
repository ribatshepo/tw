using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace USP.Infrastructure.Services.Secrets.DatabaseConnectors;

/// <summary>
/// SQL Server database connector for dynamic credential generation
/// </summary>
public class SqlServerDatabaseConnector : BaseDatabaseConnector
{
    private readonly ILogger<SqlServerDatabaseConnector> _logger;

    public override string PluginName => "sqlserver";

    public SqlServerDatabaseConnector(ILogger<SqlServerDatabaseConnector> logger)
    {
        _logger = logger;
    }

    public override async Task<bool> VerifyConnectionAsync(string connectionUrl, string? username, string? password)
    {
        try
        {
            await using var connection = new SqlConnection(connectionUrl);
            await connection.OpenAsync();

            await using var command = new SqlCommand("SELECT 1;", connection);
            await command.ExecuteScalarAsync();

            _logger.LogDebug("SQL Server connection verified successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to verify SQL Server connection");
            return false;
        }
    }

    public override async Task<(string username, string password)> CreateDynamicUserAsync(
        string connectionUrl,
        string adminUsername,
        string adminPassword,
        string creationStatements,
        int ttlSeconds)
    {
        // SQL Server usernames limited to 128 characters
        var username = GenerateUsername("dynuser").Substring(0, Math.Min(128, GenerateUsername("dynuser").Length));
        var password = GeneratePassword();

        try
        {
            await using var connection = new SqlConnection(connectionUrl);
            await connection.OpenAsync();

            // Parse creation statements for roles/permissions
            // Expected format: roles=db_datareader,db_datawriter or custom SQL
            var roles = ParseSqlServerRoles(creationStatements);

            // Create login using parameterized dynamic SQL
            var createLoginSql = @"
                DECLARE @sql NVARCHAR(MAX);
                SET @sql = N'CREATE LOGIN [' + @username + N'] WITH PASSWORD = @password;';
                EXEC sp_executesql @sql, N'@password NVARCHAR(128)', @password = @password;";

            await using var createLoginCmd = new SqlCommand(createLoginSql, connection);
            createLoginCmd.Parameters.AddWithValue("@username", username);
            createLoginCmd.Parameters.AddWithValue("@password", password);
            await createLoginCmd.ExecuteNonQueryAsync();

            // Create user in current database using parameterized dynamic SQL
            var createUserSql = @"
                DECLARE @sql NVARCHAR(MAX);
                SET @sql = N'CREATE USER [' + @username + N'] FOR LOGIN [' + @username + N'];';
                EXEC sp_executesql @sql;";

            await using var createUserCmd = new SqlCommand(createUserSql, connection);
            createUserCmd.Parameters.AddWithValue("@username", username);
            await createUserCmd.ExecuteNonQueryAsync();

            // Grant roles using parameterized dynamic SQL
            foreach (var role in roles)
            {
                var grantRoleSql = @"
                    DECLARE @sql NVARCHAR(MAX);
                    SET @sql = N'ALTER ROLE [' + @role + N'] ADD MEMBER [' + @username + N'];';
                    EXEC sp_executesql @sql;";

                await using var grantRoleCmd = new SqlCommand(grantRoleSql, connection);
                grantRoleCmd.Parameters.AddWithValue("@username", username);
                grantRoleCmd.Parameters.AddWithValue("@role", role);
                await grantRoleCmd.ExecuteNonQueryAsync();
            }

            // Verify user was created
            await using var verifyCmd = new SqlCommand(
                "SELECT 1 FROM sys.database_principals WHERE name = @username",
                connection);
            verifyCmd.Parameters.AddWithValue("@username", username);

            var result = await verifyCmd.ExecuteScalarAsync();
            if (result == null)
            {
                throw new InvalidOperationException($"User {username} was not created successfully");
            }

            _logger.LogInformation("Created SQL Server dynamic user: {Username} with roles: {Roles}",
                username, string.Join(", ", roles));
            return (username, password);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create SQL Server dynamic user");
            throw new InvalidOperationException($"Failed to create dynamic user: {ex.Message}", ex);
        }
    }

    public override async Task<bool> RevokeDynamicUserAsync(
        string connectionUrl,
        string adminUsername,
        string adminPassword,
        string username,
        string? revocationStatements)
    {
        try
        {
            await using var connection = new SqlConnection(connectionUrl);
            await connection.OpenAsync();

            // Kill active sessions for the user
            var killSql = @"
                DECLARE @kill NVARCHAR(MAX) = N'';
                SELECT @kill = @kill + N'KILL ' + CAST(session_id AS NVARCHAR(10)) + N'; '
                FROM sys.dm_exec_sessions
                WHERE login_name = @username;
                IF LEN(@kill) > 0
                    EXEC sp_executesql @kill;";

            await using var killCmd = new SqlCommand(killSql, connection);
            killCmd.Parameters.AddWithValue("@username", username);

            try
            {
                await killCmd.ExecuteNonQueryAsync();
            }
            catch
            {
                // Ignore errors killing sessions - user may not have active sessions
            }

            // Drop user from database using parameterized dynamic SQL
            var dropUserSql = @"
                DECLARE @sql NVARCHAR(MAX);
                SET @sql = N'DROP USER IF EXISTS [' + @username + N'];';
                EXEC sp_executesql @sql;";

            await using var dropUserCmd = new SqlCommand(dropUserSql, connection);
            dropUserCmd.Parameters.AddWithValue("@username", username);
            await dropUserCmd.ExecuteNonQueryAsync();

            // Drop login from server using parameterized dynamic SQL
            var dropLoginSql = @"
                DECLARE @sql NVARCHAR(MAX);
                SET @sql = N'DROP LOGIN IF EXISTS [' + @username + N'];';
                EXEC sp_executesql @sql;";

            await using var dropLoginCmd = new SqlCommand(dropLoginSql, connection);
            dropLoginCmd.Parameters.AddWithValue("@username", username);
            await dropLoginCmd.ExecuteNonQueryAsync();

            _logger.LogInformation("Revoked SQL Server dynamic user: {Username}", username);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to revoke SQL Server dynamic user: {Username}", username);
            return false;
        }
    }

    public override async Task<string> RotateRootCredentialsAsync(
        string connectionUrl,
        string currentUsername,
        string currentPassword,
        string newPassword)
    {
        try
        {
            await using var connection = new SqlConnection(connectionUrl);
            await connection.OpenAsync();

            // Use parameterized query to prevent SQL injection
            // Note: SQL Server requires dynamic SQL for ALTER LOGIN with variable password
            var sql = @"
                DECLARE @sql NVARCHAR(MAX);
                SET @sql = N'ALTER LOGIN [' + @username + N'] WITH PASSWORD = @password;';
                EXEC sp_executesql @sql, N'@password NVARCHAR(128)', @password = @newPassword;";

            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@username", currentUsername);
            command.Parameters.AddWithValue("@newPassword", newPassword);
            await command.ExecuteNonQueryAsync();

            // Verify new credentials work
            var newConnectionUrl = connectionUrl.Replace(currentPassword, newPassword);
            if (!await VerifyConnectionAsync(newConnectionUrl, currentUsername, newPassword))
            {
                throw new InvalidOperationException("New credentials failed verification");
            }

            _logger.LogInformation("Rotated SQL Server root credentials for user: {Username}", currentUsername);
            return newPassword;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rotate SQL Server root credentials");
            throw new InvalidOperationException($"Failed to rotate root credentials: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Parse SQL Server roles from creation statements
    /// </summary>
    private string[] ParseSqlServerRoles(string creationStatements)
    {
        // Default to read-only role
        var defaultRoles = new[] { "db_datareader" };

        try
        {
            if (string.IsNullOrWhiteSpace(creationStatements))
            {
                return defaultRoles;
            }

            // Check for roles= format
            if (creationStatements.Contains("roles=", StringComparison.OrdinalIgnoreCase))
            {
                var startIndex = creationStatements.IndexOf("roles=", StringComparison.OrdinalIgnoreCase) + 6;
                var rolesString = creationStatements.Substring(startIndex);

                // Take until first whitespace or semicolon
                var endIndex = rolesString.IndexOfAny(new[] { ' ', '\t', '\n', '\r', ';' });
                if (endIndex > 0)
                {
                    rolesString = rolesString.Substring(0, endIndex);
                }

                return rolesString.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(r => r.Trim())
                    .Where(r => !string.IsNullOrWhiteSpace(r))
                    .ToArray();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse SQL Server roles, using defaults");
        }

        return defaultRoles;
    }
}
