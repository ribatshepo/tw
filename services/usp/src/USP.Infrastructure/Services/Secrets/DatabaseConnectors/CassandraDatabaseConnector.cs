using Cassandra;
using Microsoft.Extensions.Logging;

namespace USP.Infrastructure.Services.Secrets.DatabaseConnectors;

/// <summary>
/// Cassandra database connector for dynamic credential generation
/// </summary>
public class CassandraDatabaseConnector : BaseDatabaseConnector
{
    private readonly ILogger<CassandraDatabaseConnector> _logger;

    public override string PluginName => "cassandra";

    public CassandraDatabaseConnector(ILogger<CassandraDatabaseConnector> logger)
    {
        _logger = logger;
    }

    public override async Task<bool> VerifyConnectionAsync(string connectionUrl, string? username, string? password)
    {
        try
        {
            var cluster = Cluster.Builder()
                .AddContactPoint(connectionUrl)
                .WithCredentials(username ?? "cassandra", password ?? "cassandra")
                .Build();

            var session = await cluster.ConnectAsync();
            var result = await session.ExecuteAsync(new SimpleStatement("SELECT release_version FROM system.local"));

            _logger.LogDebug("Cassandra connection verified successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to verify Cassandra connection");
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
            var cluster = Cluster.Builder()
                .AddContactPoint(connectionUrl)
                .WithCredentials(adminUsername, adminPassword)
                .Build();

            var session = await cluster.ConnectAsync();

            var statements = ReplacePlaceholders(creationStatements, username, password);
            var cqlCommands = statements.Split(';', StringSplitOptions.RemoveEmptyEntries);

            foreach (var cql in cqlCommands)
            {
                var trimmedCql = cql.Trim();
                if (string.IsNullOrWhiteSpace(trimmedCql)) continue;

                await session.ExecuteAsync(new SimpleStatement(trimmedCql));
            }

            _logger.LogInformation("Created Cassandra dynamic user: {Username}", username);
            return (username, password);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Cassandra dynamic user");
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
            var cluster = Cluster.Builder()
                .AddContactPoint(connectionUrl)
                .WithCredentials(adminUsername, adminPassword)
                .Build();

            var session = await cluster.ConnectAsync();

            if (!string.IsNullOrWhiteSpace(revocationStatements))
            {
                var statements = ReplacePlaceholders(revocationStatements, username, string.Empty);
                var cqlCommands = statements.Split(';', StringSplitOptions.RemoveEmptyEntries);

                foreach (var cql in cqlCommands)
                {
                    var trimmedCql = cql.Trim();
                    if (string.IsNullOrWhiteSpace(trimmedCql)) continue;

                    await session.ExecuteAsync(new SimpleStatement(trimmedCql));
                }
            }
            else
            {
                await session.ExecuteAsync(new SimpleStatement($"DROP USER IF EXISTS {username}"));
            }

            _logger.LogInformation("Revoked Cassandra dynamic user: {Username}", username);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to revoke Cassandra dynamic user: {Username}", username);
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
            var cluster = Cluster.Builder()
                .AddContactPoint(connectionUrl)
                .WithCredentials(currentUsername, currentPassword)
                .Build();

            var session = await cluster.ConnectAsync();

            var sql = $"ALTER USER {currentUsername} WITH PASSWORD '{newPassword}'";
            await session.ExecuteAsync(new SimpleStatement(sql));

            if (!await VerifyConnectionAsync(connectionUrl, currentUsername, newPassword))
            {
                throw new InvalidOperationException("New credentials failed verification");
            }

            _logger.LogInformation("Rotated Cassandra root credentials for user: {Username}", currentUsername);
            return newPassword;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rotate Cassandra root credentials");
            throw new InvalidOperationException($"Failed to rotate root credentials: {ex.Message}", ex);
        }
    }
}
