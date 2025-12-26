namespace USP.Core.Models.Configuration;

/// <summary>
/// Redis cache configuration settings
/// </summary>
public class RedisSettings
{
    /// <summary>
    /// Redis server host
    /// </summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>
    /// Redis server port
    /// </summary>
    public int Port { get; set; } = 6379;

    /// <summary>
    /// Redis password (loaded from secure configuration)
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Redis database number (0-15)
    /// </summary>
    public int Database { get; set; } = 0;

    /// <summary>
    /// Enable SSL/TLS for Redis connection
    /// </summary>
    public bool UseSsl { get; set; } = false;

    /// <summary>
    /// Connection timeout in milliseconds
    /// </summary>
    public int ConnectTimeout { get; set; } = 5000;

    /// <summary>
    /// Sync timeout in milliseconds
    /// </summary>
    public int SyncTimeout { get; set; } = 5000;

    /// <summary>
    /// Allow admin commands
    /// </summary>
    public bool AllowAdmin { get; set; } = false;

    /// <summary>
    /// Instance name prefix for cache keys
    /// </summary>
    public string InstanceName { get; set; } = "USP:";

    /// <summary>
    /// Builds a Redis connection string from configured settings
    /// </summary>
    /// <returns>Redis connection string for StackExchange.Redis</returns>
    /// <exception cref="InvalidOperationException">Thrown when required settings are missing</exception>
    public string BuildConnectionString()
    {
        if (string.IsNullOrWhiteSpace(Host))
        {
            throw new InvalidOperationException("Redis host is required. Set Redis:Host in configuration.");
        }

        var connectionStringBuilder = new System.Text.StringBuilder();
        connectionStringBuilder.Append($"{Host}:{Port}");

        if (!string.IsNullOrWhiteSpace(Password))
        {
            connectionStringBuilder.Append($",password={Password}");
        }

        connectionStringBuilder.Append($",defaultDatabase={Database}");
        connectionStringBuilder.Append($",connectTimeout={ConnectTimeout}");
        connectionStringBuilder.Append($",syncTimeout={SyncTimeout}");
        connectionStringBuilder.Append($",ssl={UseSsl.ToString().ToLower()}");
        connectionStringBuilder.Append($",allowAdmin={AllowAdmin.ToString().ToLower()}");
        connectionStringBuilder.Append(",abortConnect=false");

        return connectionStringBuilder.ToString();
    }

    /// <summary>
    /// Validates Redis configuration settings
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when validation fails</exception>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Host))
        {
            throw new InvalidOperationException("Redis host cannot be empty");
        }

        if (Port <= 0 || Port > 65535)
        {
            throw new InvalidOperationException("Redis port must be between 1 and 65535");
        }

        if (Database < 0 || Database > 15)
        {
            throw new InvalidOperationException("Redis database must be between 0 and 15");
        }

        if (ConnectTimeout < 0)
        {
            throw new InvalidOperationException("Redis connect timeout cannot be negative");
        }

        if (SyncTimeout < 0)
        {
            throw new InvalidOperationException("Redis sync timeout cannot be negative");
        }

        if (string.IsNullOrWhiteSpace(InstanceName))
        {
            throw new InvalidOperationException("Redis instance name cannot be empty");
        }
    }
}
