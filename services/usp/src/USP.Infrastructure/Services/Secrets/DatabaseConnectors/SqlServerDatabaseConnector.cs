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

            // Replace placeholders in creation statements
            var statements = ReplacePlaceholders(creationStatements, username, password);

            // Execute creation statements
            var sqlCommands = statements.Split(new[] { "GO", ";" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var sql in sqlCommands)
            {
                var trimmedSql = sql.Trim();
                if (string.IsNullOrWhiteSpace(trimmedSql)) continue;

                await using var command = new SqlCommand(trimmedSql, connection);
                await command.ExecuteNonQueryAsync();
            }

            // Verify user was created
            await using var verifyCmd = new SqlCommand(
                "SELECT 1 FROM sys.database_principals WHERE name = @username",
                connection);
            verifyCmd.Parameters.AddWithValue("username", username);

            var result = await verifyCmd.ExecuteScalarAsync();
            if (result == null)
            {
                throw new InvalidOperationException($"User {username} was not created successfully");
            }

            _logger.LogInformation("Created SQL Server dynamic user: {Username}", username);
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

            if (!string.IsNullOrWhiteSpace(revocationStatements))
            {
                // Use custom revocation statements
                var statements = ReplacePlaceholders(revocationStatements, username, string.Empty);
                var sqlCommands = statements.Split(new[] { "GO", ";" }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var sql in sqlCommands)
                {
                    var trimmedSql = sql.Trim();
                    if (string.IsNullOrWhiteSpace(trimmedSql)) continue;

                    await using var command = new SqlCommand(trimmedSql, connection);
                    await command.ExecuteNonQueryAsync();
                }
            }
            else
            {
                // Default revocation: kill sessions and drop user
                await using var killCmd = new SqlCommand(
                    @"DECLARE @kill VARCHAR(MAX) = '';
                      SELECT @kill = @kill + 'KILL ' + CAST(session_id AS VARCHAR(10)) + '; '
                      FROM sys.dm_exec_sessions
                      WHERE login_name = @username;
                      EXEC(@kill);",
                    connection);
                killCmd.Parameters.AddWithValue("username", username);

                try
                {
                    await killCmd.ExecuteNonQueryAsync();
                }
                catch
                {
                    // Ignore errors killing sessions
                }

                // Drop user from database
                await using var dropUserCmd = new SqlCommand($"DROP USER IF EXISTS [{username}];", connection);
                await dropUserCmd.ExecuteNonQueryAsync();

                // Drop login from server
                await using var dropLoginCmd = new SqlCommand($"DROP LOGIN IF EXISTS [{username}];", connection);
                await dropLoginCmd.ExecuteNonQueryAsync();
            }

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

            var sql = $"ALTER LOGIN [{currentUsername}] WITH PASSWORD = '{newPassword}';";

            await using var command = new SqlCommand(sql, connection);
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
}
