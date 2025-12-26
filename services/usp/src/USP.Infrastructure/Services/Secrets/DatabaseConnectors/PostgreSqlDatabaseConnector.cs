using Microsoft.Extensions.Logging;
using Npgsql;

namespace USP.Infrastructure.Services.Secrets.DatabaseConnectors;

/// <summary>
/// PostgreSQL database connector for dynamic credential generation
/// </summary>
public class PostgreSqlDatabaseConnector : BaseDatabaseConnector
{
    private readonly ILogger<PostgreSqlDatabaseConnector> _logger;

    public override string PluginName => "postgresql";

    public PostgreSqlDatabaseConnector(ILogger<PostgreSqlDatabaseConnector> logger)
    {
        _logger = logger;
    }

    public override async Task<bool> VerifyConnectionAsync(string connectionUrl, string? username, string? password)
    {
        try
        {
            await using var connection = new NpgsqlConnection(connectionUrl);
            await connection.OpenAsync();

            await using var command = new NpgsqlCommand("SELECT 1;", connection);
            await command.ExecuteScalarAsync();

            _logger.LogDebug("PostgreSQL connection verified successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to verify PostgreSQL connection");
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
        var username = GenerateUsername("dynuser");
        var password = GeneratePassword();

        try
        {
            await using var connection = new NpgsqlConnection(connectionUrl);
            await connection.OpenAsync();

            // Replace placeholders in creation statements
            var statements = ReplacePlaceholders(creationStatements, username, password);

            // Execute creation statements
            var sqlCommands = statements.Split(';', StringSplitOptions.RemoveEmptyEntries);

            foreach (var sql in sqlCommands)
            {
                var trimmedSql = sql.Trim();
                if (string.IsNullOrWhiteSpace(trimmedSql)) continue;

                await using var command = new NpgsqlCommand(trimmedSql, connection);
                await command.ExecuteNonQueryAsync();
            }

            // Verify user was created
            await using var verifyCmd = new NpgsqlCommand(
                "SELECT 1 FROM pg_roles WHERE rolname = @username",
                connection);
            verifyCmd.Parameters.AddWithValue("username", username);

            var result = await verifyCmd.ExecuteScalarAsync();
            if (result == null)
            {
                throw new InvalidOperationException($"User {username} was not created successfully");
            }

            _logger.LogInformation("Created PostgreSQL dynamic user: {Username}", username);
            return (username, password);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create PostgreSQL dynamic user");
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
            await using var connection = new NpgsqlConnection(connectionUrl);
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

                    await using var command = new NpgsqlCommand(trimmedSql, connection);
                    await command.ExecuteNonQueryAsync();
                }
            }
            else
            {
                // Default revocation: terminate connections and drop user
                await using var terminateCmd = new NpgsqlCommand(
                    @"SELECT pg_terminate_backend(pg_stat_activity.pid)
                      FROM pg_stat_activity
                      WHERE pg_stat_activity.usename = @username",
                    connection);
                terminateCmd.Parameters.AddWithValue("username", username);
                await terminateCmd.ExecuteNonQueryAsync();

                // Revoke privileges and drop user
                await using var dropCmd = new NpgsqlCommand(
                    $@"REVOKE ALL PRIVILEGES ON ALL TABLES IN SCHEMA public FROM ""{username}"";
                       REVOKE ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA public FROM ""{username}"";
                       REVOKE ALL PRIVILEGES ON ALL FUNCTIONS IN SCHEMA public FROM ""{username}"";
                       DROP USER IF EXISTS ""{username}"";",
                    connection);
                await dropCmd.ExecuteNonQueryAsync();
            }

            _logger.LogInformation("Revoked PostgreSQL dynamic user: {Username}", username);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to revoke PostgreSQL dynamic user: {Username}", username);
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
            await using var connection = new NpgsqlConnection(connectionUrl);
            await connection.OpenAsync();

            var escapedPassword = newPassword.Replace("'", "''");
            var sql = $"ALTER USER \"{currentUsername}\" WITH PASSWORD '{escapedPassword}';";

            await using var command = new NpgsqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync();

            // Verify new credentials work
            var newConnectionUrl = connectionUrl.Replace(currentPassword, newPassword);
            if (!await VerifyConnectionAsync(newConnectionUrl, currentUsername, newPassword))
            {
                throw new InvalidOperationException("New credentials failed verification");
            }

            _logger.LogInformation("Rotated PostgreSQL root credentials for user: {Username}", currentUsername);
            return newPassword;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rotate PostgreSQL root credentials");
            throw new InvalidOperationException($"Failed to rotate root credentials: {ex.Message}", ex);
        }
    }
}
