using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using USP.Core.Services.PAM;

namespace USP.Infrastructure.Services.PAM.Connectors;

/// <summary>
/// Redis admin password rotation connector
/// </summary>
public class RedisConnector : BaseConnector
{
    private readonly ILogger<RedisConnector> _logger;

    public override string Platform => "Redis";

    public RedisConnector(ILogger<RedisConnector> logger)
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

        ConnectionMultiplexer? redis = null;

        try
        {
            // Build connection string with current password
            var configOptions = new ConfigurationOptions
            {
                EndPoints = { $"{hostAddress}:{port ?? 6379}" },
                Password = currentPassword,
                ConnectTimeout = 10000,
                SyncTimeout = 10000,
                AbortOnConnectFail = false
            };

            redis = await ConnectionMultiplexer.ConnectAsync(configOptions);

            if (!redis.IsConnected)
            {
                result.ErrorMessage = "Failed to connect to Redis server";
                return result;
            }

            var server = redis.GetServer(redis.GetEndPoints().First());

            // Check if ACL is supported (Redis 6+)
            var info = await server.InfoAsync("server");
            var versionString = info.FirstOrDefault(x => x.Key == "redis_version").Value;

            if (!string.IsNullOrEmpty(versionString))
            {
                var version = ParseRedisVersion(versionString);

                if (version.Major >= 6 && !string.IsNullOrEmpty(username) && username != "default")
                {
                    // Use ACL SETUSER for Redis 6+ with named users
                    await server.ExecuteAsync("ACL", "SETUSER", username, ">", newPassword);

                    _logger.LogInformation(
                        "Password rotated using ACL for Redis user {Username} on {Host}",
                        username,
                        hostAddress);
                }
                else
                {
                    // Use CONFIG SET requirepass for default user or Redis < 6
                    await server.ExecuteAsync("CONFIG", "SET", "requirepass", newPassword);

                    // Rewrite config to persist changes
                    await server.ExecuteAsync("CONFIG", "REWRITE");

                    _logger.LogInformation(
                        "Password rotated using CONFIG SET for Redis on {Host}",
                        hostAddress);
                }
            }
            else
            {
                // Fallback to CONFIG SET if version detection fails
                await server.ExecuteAsync("CONFIG", "SET", "requirepass", newPassword);
                await server.ExecuteAsync("CONFIG", "REWRITE");
            }

            result.Success = true;
            result.Details = $"Password rotated successfully for Redis on {hostAddress}";

            _logger.LogInformation(
                "Password rotated successfully for Redis on {Host}",
                hostAddress);
        }
        catch (RedisConnectionException ex)
        {
            result.ErrorMessage = "Unable to connect to Redis server";
            result.Details = $"Connection failed: {ex.Message}";

            _logger.LogError(ex,
                "Connection failed to Redis server {Host}",
                hostAddress);
        }
        catch (RedisCommandException ex)
        {
            result.ErrorMessage = "Failed to execute Redis command";
            result.Details = $"Command failed: {ex.Message}";

            _logger.LogError(ex,
                "Failed to rotate password for Redis on {Host}",
                hostAddress);
        }
        catch (Exception ex)
        {
            result.ErrorMessage = ex.Message;
            result.Details = $"Failed to rotate password: {ex.Message}";

            _logger.LogError(ex,
                "Unexpected error rotating password for Redis on {Host}",
                hostAddress);
        }
        finally
        {
            redis?.Dispose();
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
        ConnectionMultiplexer? redis = null;

        try
        {
            var configOptions = new ConfigurationOptions
            {
                EndPoints = { $"{hostAddress}:{port ?? 6379}" },
                Password = password,
                ConnectTimeout = 10000,
                SyncTimeout = 10000,
                AbortOnConnectFail = false
            };

            redis = await ConnectionMultiplexer.ConnectAsync(configOptions);

            if (!redis.IsConnected)
            {
                return false;
            }

            // Execute PING to verify connection
            var db = redis.GetDatabase();
            var pong = await db.PingAsync();

            _logger.LogDebug(
                "Credentials verified successfully for Redis on {Host} (ping: {Ping}ms)",
                hostAddress,
                pong.TotalMilliseconds);

            return true;
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogWarning(ex,
                "Failed to verify credentials for Redis on {Host}",
                hostAddress);

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Error verifying credentials for Redis on {Host}",
                hostAddress);

            return false;
        }
        finally
        {
            redis?.Dispose();
        }
    }

    /// <summary>
    /// Get Redis server information
    /// </summary>
    public async Task<Dictionary<string, string>> GetServerInfoAsync(
        string hostAddress,
        int? port,
        string password)
    {
        var info = new Dictionary<string, string>();
        ConnectionMultiplexer? redis = null;

        try
        {
            var configOptions = new ConfigurationOptions
            {
                EndPoints = { $"{hostAddress}:{port ?? 6379}" },
                Password = password,
                ConnectTimeout = 10000,
                SyncTimeout = 10000,
                AbortOnConnectFail = false
            };

            redis = await ConnectionMultiplexer.ConnectAsync(configOptions);

            if (redis.IsConnected)
            {
                var server = redis.GetServer(redis.GetEndPoints().First());
                var serverInfo = await server.InfoAsync("server");

                foreach (var entry in serverInfo)
                {
                    info[entry.Key] = entry.Value;
                }

                _logger.LogDebug(
                    "Retrieved server info for Redis on {Host}",
                    hostAddress);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to get server info for Redis on {Host}",
                hostAddress);
        }
        finally
        {
            redis?.Dispose();
        }

        return info;
    }

    /// <summary>
    /// List ACL users (Redis 6+)
    /// </summary>
    public async Task<List<string>> ListAclUsersAsync(
        string hostAddress,
        int? port,
        string password)
    {
        var users = new List<string>();
        ConnectionMultiplexer? redis = null;

        try
        {
            var configOptions = new ConfigurationOptions
            {
                EndPoints = { $"{hostAddress}:{port ?? 6379}" },
                Password = password,
                ConnectTimeout = 10000,
                SyncTimeout = 10000,
                AbortOnConnectFail = false
            };

            redis = await ConnectionMultiplexer.ConnectAsync(configOptions);

            if (redis.IsConnected)
            {
                var server = redis.GetServer(redis.GetEndPoints().First());

                try
                {
                    var result = await server.ExecuteAsync("ACL", "USERS");

                    if (result.Type == ResultType.MultiBulk)
                    {
                        var userArray = (RedisResult[])result!;
                        users.AddRange(userArray.Select(u => u.ToString()));
                    }

                    _logger.LogInformation(
                        "Listed {Count} ACL users on Redis server {Host}",
                        users.Count,
                        hostAddress);
                }
                catch (RedisCommandException)
                {
                    // ACL not supported, likely Redis < 6
                    _logger.LogWarning(
                        "ACL commands not supported on Redis server {Host} (likely version < 6.0)",
                        hostAddress);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to list ACL users for Redis on {Host}",
                hostAddress);
        }
        finally
        {
            redis?.Dispose();
        }

        return users;
    }

    private static Version ParseRedisVersion(string versionString)
    {
        try
        {
            // Redis version format: "6.2.7" or "7.0.0"
            var parts = versionString.Split('.');
            if (parts.Length >= 2)
            {
                var major = int.Parse(parts[0]);
                var minor = int.Parse(parts[1]);
                return new Version(major, minor);
            }
        }
        catch
        {
            // Ignore parsing errors
        }

        return new Version(0, 0);
    }
}
