using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace USP.Infrastructure.Services.Secrets.DatabaseConnectors;

/// <summary>
/// MongoDB database connector for dynamic credential generation
/// </summary>
public class MongoDbDatabaseConnector : BaseDatabaseConnector
{
    private readonly ILogger<MongoDbDatabaseConnector> _logger;

    public override string PluginName => "mongodb";

    public MongoDbDatabaseConnector(ILogger<MongoDbDatabaseConnector> logger)
    {
        _logger = logger;
    }

    public override async Task<bool> VerifyConnectionAsync(string connectionUrl, string? username, string? password)
    {
        try
        {
            var client = new MongoClient(connectionUrl);
            var database = client.GetDatabase("admin");

            await database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));

            _logger.LogDebug("MongoDB connection verified successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to verify MongoDB connection");
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
            var client = new MongoClient(connectionUrl);

            // Parse creation statements to determine database and roles
            // Expected format in statements: db=<database>,roles=[{role:"readWrite",db:"<database>"}]
            var (dbName, roles) = ParseMongoCreationStatements(creationStatements);

            var database = client.GetDatabase(dbName);

            // Create user with specified roles
            var createUserCommand = new BsonDocument
            {
                { "createUser", username },
                { "pwd", password },
                { "roles", roles }
            };

            await database.RunCommandAsync<BsonDocument>(createUserCommand);

            _logger.LogInformation("Created MongoDB dynamic user: {Username} in database: {Database}", username, dbName);
            return (username, password);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create MongoDB dynamic user");
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
            var client = new MongoClient(connectionUrl);

            // Get all databases where user might exist
            var adminDb = client.GetDatabase("admin");

            // Try to drop user from admin database (most common location)
            try
            {
                var dropUserCommand = new BsonDocument("dropUser", username);
                await adminDb.RunCommandAsync<BsonDocument>(dropUserCommand);
            }
            catch
            {
                // User might not exist in admin database
            }

            // Try to drop from other common databases
            var commonDbs = new[] { "admin", "test", "local" };
            foreach (var dbName in commonDbs)
            {
                try
                {
                    var db = client.GetDatabase(dbName);
                    var dropUserCommand = new BsonDocument("dropUser", username);
                    await db.RunCommandAsync<BsonDocument>(dropUserCommand);
                }
                catch
                {
                    // Ignore if user doesn't exist in this database
                }
            }

            _logger.LogInformation("Revoked MongoDB dynamic user: {Username}", username);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to revoke MongoDB dynamic user: {Username}", username);
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
            var client = new MongoClient(connectionUrl);
            var database = client.GetDatabase("admin");

            // Update user password
            var updateUserCommand = new BsonDocument
            {
                { "updateUser", currentUsername },
                { "pwd", newPassword }
            };

            await database.RunCommandAsync<BsonDocument>(updateUserCommand);

            // Verify new credentials work
            var newConnectionUrl = connectionUrl.Replace(currentPassword, newPassword);
            if (!await VerifyConnectionAsync(newConnectionUrl, currentUsername, newPassword))
            {
                throw new InvalidOperationException("New credentials failed verification");
            }

            _logger.LogInformation("Rotated MongoDB root credentials for user: {Username}", currentUsername);
            return newPassword;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rotate MongoDB root credentials");
            throw new InvalidOperationException($"Failed to rotate root credentials: {ex.Message}", ex);
        }
    }

    private (string dbName, BsonArray roles) ParseMongoCreationStatements(string creationStatements)
    {
        // Default to admin database with readWrite role
        var dbName = "admin";
        var roles = new BsonArray
        {
            new BsonDocument { { "role", "readWrite" }, { "db", "admin" } }
        };

        try
        {
            // Parse simple format: db=mydb,roles=[{role:"readWrite",db:"mydb"}]
            if (creationStatements.Contains("db="))
            {
                var parts = creationStatements.Split(',');
                foreach (var part in parts)
                {
                    if (part.Trim().StartsWith("db="))
                    {
                        dbName = part.Substring(part.IndexOf('=') + 1).Trim().Trim('"', '\'');
                    }
                    else if (part.Trim().StartsWith("roles="))
                    {
                        // Parse roles JSON
                        var rolesJson = part.Substring(part.IndexOf('=') + 1).Trim();
                        roles = BsonSerializer.Deserialize<BsonArray>(rolesJson);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse MongoDB creation statements, using defaults");
        }

        return (dbName, roles);
    }
}
