using Microsoft.Extensions.Logging;
using Oracle.ManagedDataAccess.Client;

namespace USP.Infrastructure.Services.Secrets.DatabaseConnectors;

/// <summary>
/// Oracle database connector for dynamic credential generation
/// </summary>
public class OracleDatabaseConnector : BaseDatabaseConnector
{
    private readonly ILogger<OracleDatabaseConnector> _logger;

    public override string PluginName => "oracle";

    public OracleDatabaseConnector(ILogger<OracleDatabaseConnector> logger)
    {
        _logger = logger;
    }

    public override async Task<bool> VerifyConnectionAsync(string connectionUrl, string? username, string? password)
    {
        try
        {
            using var connection = new OracleConnection(connectionUrl);
            await connection.OpenAsync();

            using var command = new OracleCommand("SELECT 1 FROM DUAL", connection);
            await command.ExecuteScalarAsync();

            _logger.LogDebug("Oracle connection verified successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to verify Oracle connection");
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
        var username = GenerateUsername("dynuser").ToUpper();
        var password = GeneratePassword();

        try
        {
            using var connection = new OracleConnection(connectionUrl);
            await connection.OpenAsync();

            var statements = ReplacePlaceholders(creationStatements, username, password);
            var sqlCommands = statements.Split(';', StringSplitOptions.RemoveEmptyEntries);

            foreach (var sql in sqlCommands)
            {
                var trimmedSql = sql.Trim();
                if (string.IsNullOrWhiteSpace(trimmedSql)) continue;

                using var command = new OracleCommand(trimmedSql, connection);
                await command.ExecuteNonQueryAsync();
            }

            _logger.LogInformation("Created Oracle dynamic user: {Username}", username);
            return (username, password);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Oracle dynamic user");
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
            using var connection = new OracleConnection(connectionUrl);
            await connection.OpenAsync();

            if (!string.IsNullOrWhiteSpace(revocationStatements))
            {
                var statements = ReplacePlaceholders(revocationStatements, username, string.Empty);
                var sqlCommands = statements.Split(';', StringSplitOptions.RemoveEmptyEntries);

                foreach (var sql in sqlCommands)
                {
                    var trimmedSql = sql.Trim();
                    if (string.IsNullOrWhiteSpace(trimmedSql)) continue;

                    using var command = new OracleCommand(trimmedSql, connection);
                    await command.ExecuteNonQueryAsync();
                }
            }
            else
            {
                using var dropCmd = new OracleCommand($"DROP USER {username} CASCADE", connection);
                await dropCmd.ExecuteNonQueryAsync();
            }

            _logger.LogInformation("Revoked Oracle dynamic user: {Username}", username);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to revoke Oracle dynamic user: {Username}", username);
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
            using var connection = new OracleConnection(connectionUrl);
            await connection.OpenAsync();

            var sql = $"ALTER USER {currentUsername} IDENTIFIED BY \"{newPassword}\"";

            using var command = new OracleCommand(sql, connection);
            await command.ExecuteNonQueryAsync();

            var newConnectionUrl = connectionUrl.Replace(currentPassword, newPassword);
            if (!await VerifyConnectionAsync(newConnectionUrl, currentUsername, newPassword))
            {
                throw new InvalidOperationException("New credentials failed verification");
            }

            _logger.LogInformation("Rotated Oracle root credentials for user: {Username}", currentUsername);
            return newPassword;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rotate Oracle root credentials");
            throw new InvalidOperationException($"Failed to rotate root credentials: {ex.Message}", ex);
        }
    }
}
