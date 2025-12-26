using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace USP.Infrastructure.Services.Secrets.DatabaseConnectors;

/// <summary>
/// Redis database connector for password rotation
/// Note: Redis doesn't support multiple users until Redis 6.0+ with ACLs
/// </summary>
public class RedisDatabaseConnector : BaseDatabaseConnector
{
    private readonly ILogger<RedisDatabaseConnector> _logger;

    public override string PluginName => "redis";

    public RedisDatabaseConnector(ILogger<RedisDatabaseConnector> logger)
    {
        _logger = logger;
    }

    public override async Task<bool> VerifyConnectionAsync(string connectionUrl, string? username, string? password)
    {
        try
        {
            var connection = await ConnectionMultiplexer.ConnectAsync(connectionUrl);
            var db = connection.GetDatabase();

            await db.PingAsync();

            _logger.LogDebug("Redis connection verified successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to verify Redis connection");
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
            var connection = await ConnectionMultiplexer.ConnectAsync(connectionUrl);
            var db = connection.GetDatabase();

            // Redis 6.0+ ACL support
            // Parse creation statements for ACL rules
            // Expected format: ACL SETUSER {{username}} on >{{password}} ~* +@all
            var aclRule = ReplacePlaceholders(creationStatements, username, password);

            // Execute ACL command
            var server = connection.GetServer(connection.GetEndPoints().First());
            await server.ExecuteAsync("ACL", aclRule.Split(' '));

            _logger.LogInformation("Created Redis dynamic user: {Username}", username);
            return (username, password);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Redis dynamic user");
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
            var connection = await ConnectionMultiplexer.ConnectAsync(connectionUrl);
            var server = connection.GetServer(connection.GetEndPoints().First());

            // Delete ACL user
            await server.ExecuteAsync("ACL", "DELUSER", username);

            _logger.LogInformation("Revoked Redis dynamic user: {Username}", username);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to revoke Redis dynamic user: {Username}", username);
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
            var connection = await ConnectionMultiplexer.ConnectAsync(connectionUrl);
            var server = connection.GetServer(connection.GetEndPoints().First());

            if (string.IsNullOrEmpty(currentUsername) || currentUsername == "default")
            {
                // Rotate default user password
                await server.ExecuteAsync("CONFIG", "SET", "requirepass", newPassword);
            }
            else
            {
                // Rotate ACL user password
                await server.ExecuteAsync("ACL", "SETUSER", currentUsername, ">", newPassword);
            }

            // Verify new credentials work
            var newConnectionUrl = connectionUrl.Replace(currentPassword, newPassword);
            if (!await VerifyConnectionAsync(newConnectionUrl, currentUsername, newPassword))
            {
                throw new InvalidOperationException("New credentials failed verification");
            }

            _logger.LogInformation("Rotated Redis root credentials for user: {Username}", currentUsername ?? "default");
            return newPassword;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rotate Redis root credentials");
            throw new InvalidOperationException($"Failed to rotate root credentials: {ex.Message}", ex);
        }
    }
}
