using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using USP.Core.Services.PAM;

namespace USP.Infrastructure.Services.PAM.Connectors;

/// <summary>
/// MongoDB privileged account management connector
/// </summary>
public class MongoDbConnector : BaseConnector
{
    private readonly ILogger<MongoDbConnector> _logger;

    public override string Platform => "MongoDB";

    public MongoDbConnector(ILogger<MongoDbConnector> logger)
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

        MongoClient? client = null;

        try
        {
            // Build connection string with current credentials
            var connectionString = BuildConnectionString(
                hostAddress,
                port ?? 27017,
                username,
                currentPassword,
                databaseName ?? "admin");

            client = new MongoClient(connectionString);

            // Get admin database
            var adminDb = client.GetDatabase("admin");

            // Update user password using updateUser command
            var updateCommand = new BsonDocument
            {
                { "updateUser", username },
                { "pwd", newPassword }
            };

            await adminDb.RunCommandAsync<BsonDocument>(updateCommand);

            result.Success = true;
            result.Details = $"Password rotated successfully for MongoDB user {username} on {hostAddress}";

            _logger.LogInformation(
                "Password rotated successfully for MongoDB user {Username} on {Host}",
                username,
                hostAddress);
        }
        catch (MongoAuthenticationException ex)
        {
            result.ErrorMessage = "Authentication failed with current password";
            result.Details = $"Failed to authenticate: {ex.Message}";

            _logger.LogError(ex,
                "Authentication failed for MongoDB user {Username} on {Host}",
                username,
                hostAddress);
        }
        catch (MongoConnectionException ex)
        {
            result.ErrorMessage = "Unable to connect to MongoDB server";
            result.Details = $"Connection failed: {ex.Message}";

            _logger.LogError(ex,
                "Connection failed to MongoDB server {Host}",
                hostAddress);
        }
        catch (MongoCommandException ex)
        {
            result.ErrorMessage = "Failed to execute updateUser command";
            result.Details = $"Command failed: {ex.Message}";

            _logger.LogError(ex,
                "Failed to rotate password for MongoDB user {Username} on {Host}",
                username,
                hostAddress);
        }
        catch (Exception ex)
        {
            result.ErrorMessage = ex.Message;
            result.Details = $"Failed to rotate password: {ex.Message}";

            _logger.LogError(ex,
                "Unexpected error rotating password for MongoDB user {Username} on {Host}",
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
        MongoClient? client = null;

        try
        {
            var connectionString = BuildConnectionString(
                hostAddress,
                port ?? 27017,
                username,
                password,
                databaseName ?? "admin");

            var settings = MongoClientSettings.FromConnectionString(connectionString);
            settings.ServerSelectionTimeout = TimeSpan.FromSeconds(10);
            settings.ConnectTimeout = TimeSpan.FromSeconds(10);

            client = new MongoClient(settings);

            // Try to list databases to verify authentication
            var databases = await client.ListDatabaseNamesAsync();
            await databases.MoveNextAsync();

            _logger.LogDebug(
                "Credentials verified successfully for MongoDB user {Username} on {Host}",
                username,
                hostAddress);

            return true;
        }
        catch (MongoAuthenticationException ex)
        {
            _logger.LogWarning(ex,
                "Failed to verify credentials for MongoDB user {Username} on {Host}",
                username,
                hostAddress);

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Error verifying credentials for MongoDB user {Username} on {Host}",
                username,
                hostAddress);

            return false;
        }
    }

    /// <summary>
    /// Discover privileged accounts on MongoDB server
    /// </summary>
    public async Task<List<string>> DiscoverPrivilegedAccountsAsync(
        string hostAddress,
        int? port,
        string adminUsername,
        string adminPassword,
        string? databaseName = null)
    {
        var privilegedUsers = new List<string>();
        MongoClient? client = null;

        try
        {
            var connectionString = BuildConnectionString(
                hostAddress,
                port ?? 27017,
                adminUsername,
                adminPassword,
                "admin");

            client = new MongoClient(connectionString);

            var adminDb = client.GetDatabase("admin");

            // Get all users with admin roles
            var usersInfoCommand = new BsonDocument { { "usersInfo", 1 } };
            var usersResult = await adminDb.RunCommandAsync<BsonDocument>(usersInfoCommand);

            if (usersResult.Contains("users"))
            {
                var users = usersResult["users"].AsBsonArray;

                foreach (var user in users)
                {
                    var userDoc = user.AsBsonDocument;
                    var userName = userDoc["user"].AsString;
                    var roles = userDoc["roles"].AsBsonArray;

                    // Check if user has privileged roles
                    foreach (var role in roles)
                    {
                        var roleDoc = role.AsBsonDocument;
                        var roleName = roleDoc["role"].AsString;

                        if (IsPrivilegedRole(roleName))
                        {
                            privilegedUsers.Add(userName);
                            break;
                        }
                    }
                }
            }

            _logger.LogInformation(
                "Discovered {Count} privileged accounts on MongoDB server {Host}",
                privilegedUsers.Count,
                hostAddress);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to discover privileged accounts on MongoDB server {Host}",
                hostAddress);
        }

        return privilegedUsers;
    }

    private static string BuildConnectionString(
        string hostAddress,
        int port,
        string username,
        string password,
        string authDatabase)
    {
        return $"mongodb://{Uri.EscapeDataString(username)}:{Uri.EscapeDataString(password)}@{hostAddress}:{port}/{authDatabase}";
    }

    private static bool IsPrivilegedRole(string roleName)
    {
        var privilegedRoles = new[]
        {
            "root",
            "dbOwner",
            "userAdmin",
            "userAdminAnyDatabase",
            "dbAdminAnyDatabase",
            "readWriteAnyDatabase",
            "clusterAdmin",
            "backup",
            "restore"
        };

        return privilegedRoles.Contains(roleName, StringComparer.OrdinalIgnoreCase);
    }
}
