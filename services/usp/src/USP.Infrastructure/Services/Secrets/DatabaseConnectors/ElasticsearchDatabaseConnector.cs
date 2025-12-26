using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using Microsoft.Extensions.Logging;

namespace USP.Infrastructure.Services.Secrets.DatabaseConnectors;

/// <summary>
/// Elasticsearch database connector for user management
/// </summary>
public class ElasticsearchDatabaseConnector : BaseDatabaseConnector
{
    private readonly ILogger<ElasticsearchDatabaseConnector> _logger;

    public override string PluginName => "elasticsearch";

    public ElasticsearchDatabaseConnector(ILogger<ElasticsearchDatabaseConnector> logger)
    {
        _logger = logger;
    }

    public override async Task<bool> VerifyConnectionAsync(string connectionUrl, string? username, string? password)
    {
        try
        {
            var settings = new ElasticsearchClientSettings(new Uri(connectionUrl))
                .Authentication(new BasicAuthentication(username ?? "elastic", password ?? "changeme"));

            var client = new ElasticsearchClient(settings);
            var response = await client.PingAsync();

            _logger.LogDebug("Elasticsearch connection verified successfully");
            return response.IsValidResponse;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to verify Elasticsearch connection");
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
            var settings = new ElasticsearchClientSettings(new Uri(connectionUrl))
                .Authentication(new BasicAuthentication(adminUsername, adminPassword));

            var client = new ElasticsearchClient(settings);

            // Parse creation statements for roles
            // Expected format: roles=read,write
            var roles = ParseElasticsearchRoles(creationStatements);

            // Create user using Security API
            var createUserRequest = new
            {
                password = password,
                roles = roles,
                full_name = $"Dynamic user {username}",
                enabled = true
            };

            var response = await client.Transport.RequestAsync<object>(
                HttpMethod.Post,
                $"/_security/user/{username}",
                PostData.Serializable(createUserRequest));

            _logger.LogInformation("Created Elasticsearch dynamic user: {Username}", username);
            return (username, password);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Elasticsearch dynamic user");
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
            var settings = new ElasticsearchClientSettings(new Uri(connectionUrl))
                .Authentication(new BasicAuthentication(adminUsername, adminPassword));

            var client = new ElasticsearchClient(settings);

            await client.Transport.RequestAsync<object>(
                HttpMethod.Delete,
                $"/_security/user/{username}");

            _logger.LogInformation("Revoked Elasticsearch dynamic user: {Username}", username);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to revoke Elasticsearch dynamic user: {Username}", username);
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
            var settings = new ElasticsearchClientSettings(new Uri(connectionUrl))
                .Authentication(new BasicAuthentication(currentUsername, currentPassword));

            var client = new ElasticsearchClient(settings);

            var updatePasswordRequest = new { password = newPassword };

            await client.Transport.RequestAsync<object>(
                HttpMethod.Post,
                $"/_security/user/{currentUsername}/_password",
                PostData.Serializable(updatePasswordRequest));

            if (!await VerifyConnectionAsync(connectionUrl, currentUsername, newPassword))
            {
                throw new InvalidOperationException("New credentials failed verification");
            }

            _logger.LogInformation("Rotated Elasticsearch root credentials for user: {Username}", currentUsername);
            return newPassword;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rotate Elasticsearch root credentials");
            throw new InvalidOperationException($"Failed to rotate root credentials: {ex.Message}", ex);
        }
    }

    private string[] ParseElasticsearchRoles(string creationStatements)
    {
        // Default to read-only role
        var defaultRoles = new[] { "viewer" };

        try
        {
            if (creationStatements.Contains("roles="))
            {
                var rolesString = creationStatements
                    .Substring(creationStatements.IndexOf("roles=") + 6)
                    .Split(',')[0]
                    .Trim();

                return rolesString.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(r => r.Trim())
                    .ToArray();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse Elasticsearch roles, using defaults");
        }

        return defaultRoles;
    }
}
