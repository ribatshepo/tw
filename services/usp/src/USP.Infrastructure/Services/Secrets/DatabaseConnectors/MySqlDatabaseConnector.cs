using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace USP.Infrastructure.Services.Secrets.DatabaseConnectors;

/// <summary>
/// MySQL/MariaDB database connector for dynamic credential generation
/// </summary>
public class MySqlDatabaseConnector : BaseDatabaseConnector
{
    private readonly ILogger<MySqlDatabaseConnector> _logger;

    public override string PluginName => "mysql";

    public MySqlDatabaseConnector(ILogger<MySqlDatabaseConnector> logger)
    {
        _logger = logger;
    }

    public override async Task<bool> VerifyConnectionAsync(string connectionUrl, string? username, string? password)
    {
        try
        {
            await using var connection = new MySqlConnection(connectionUrl);
            await connection.OpenAsync();

            await using var command = new MySqlCommand("SELECT 1;", connection);
            await command.ExecuteScalarAsync();

            _logger.LogDebug("MySQL connection verified successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to verify MySQL connection");
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
        // MySQL usernames are limited to 32 characters
        var username = GenerateUsername("dynuser").Substring(0, Math.Min(32, GenerateUsername("dynuser").Length));
        var password = GeneratePassword();

        try
        {
            await using var connection = new MySqlConnection(connectionUrl);
            await connection.OpenAsync();

            // Replace placeholders in creation statements
            var statements = ReplacePlaceholders(creationStatements, username, password);

            // Execute creation statements
            var sqlCommands = statements.Split(';', StringSplitOptions.RemoveEmptyEntries);

            foreach (var sql in sqlCommands)
            {
                var trimmedSql = sql.Trim();
                if (string.IsNullOrWhiteSpace(trimmedSql)) continue;

                await using var command = new MySqlCommand(trimmedSql, connection);
                await command.ExecuteNonQueryAsync();
            }

            // Flush privileges to ensure user is active
            await using var flushCmd = new MySqlCommand("FLUSH PRIVILEGES;", connection);
            await flushCmd.ExecuteNonQueryAsync();

            // Verify user was created
            await using var verifyCmd = new MySqlCommand(
                "SELECT 1 FROM mysql.user WHERE User = @username",
                connection);
            verifyCmd.Parameters.AddWithValue("username", username);

            var result = await verifyCmd.ExecuteScalarAsync();
            if (result == null)
            {
                throw new InvalidOperationException($"User {username} was not created successfully");
            }

            _logger.LogInformation("Created MySQL dynamic user: {Username}", username);
            return (username, password);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create MySQL dynamic user");
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
            await using var connection = new MySqlConnection(connectionUrl);
            await connection.OpenAsync();

            if (!string.IsNullOrWhiteSpace(revocationStatements))
            {
                // Use custom revocation statements
                var statements = ReplacePlaceholders(revocationStatements, username, string.Empty);
                var sqlCommands = statements.Split(';', StringSplitOptions.RemoveEmptyEntries);

                foreach (var sql in sqlCommands)
                {
                    var trimmedSql = sql.Trim();
                    if (string.IsNullOrWhiteSpace(trimmedSql)) continue;

                    await using var command = new MySqlCommand(trimmedSql, connection);
                    await command.ExecuteNonQueryAsync();
                }
            }
            else
            {
                // Default revocation: kill connections and drop user
                await using var killCmd = new MySqlCommand(
                    @"SELECT CONCAT('KILL ', id, ';') AS kill_command
                      FROM INFORMATION_SCHEMA.PROCESSLIST
                      WHERE USER = @username",
                    connection);
                killCmd.Parameters.AddWithValue("username", username);

                var killCommands = new List<string>();
                await using (var reader = await killCmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        killCommands.Add(reader.GetString(0));
                    }
                }

                // Execute kill commands
                foreach (var cmd in killCommands)
                {
                    try
                    {
                        await using var killExecCmd = new MySqlCommand(cmd, connection);
                        await killExecCmd.ExecuteNonQueryAsync();
                    }
                    catch
                    {
                        // Ignore errors killing connections
                    }
                }

                // Drop user from all hosts
                await using var dropCmd = new MySqlCommand(
                    $"DROP USER IF EXISTS '{username}'@'%';",
                    connection);
                await dropCmd.ExecuteNonQueryAsync();

                await using var flushCmd = new MySqlCommand("FLUSH PRIVILEGES;", connection);
                await flushCmd.ExecuteNonQueryAsync();
            }

            _logger.LogInformation("Revoked MySQL dynamic user: {Username}", username);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to revoke MySQL dynamic user: {Username}", username);
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
            await using var connection = new MySqlConnection(connectionUrl);
            await connection.OpenAsync();

            var sql = $"ALTER USER '{currentUsername}'@'%' IDENTIFIED BY '{newPassword}';";

            await using var command = new MySqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync();

            await using var flushCmd = new MySqlCommand("FLUSH PRIVILEGES;", connection);
            await flushCmd.ExecuteNonQueryAsync();

            // Verify new credentials work
            var newConnectionUrl = connectionUrl.Replace(currentPassword, newPassword);
            if (!await VerifyConnectionAsync(newConnectionUrl, currentUsername, newPassword))
            {
                throw new InvalidOperationException("New credentials failed verification");
            }

            _logger.LogInformation("Rotated MySQL root credentials for user: {Username}", currentUsername);
            return newPassword;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rotate MySQL root credentials");
            throw new InvalidOperationException($"Failed to rotate root credentials: {ex.Message}", ex);
        }
    }
}
