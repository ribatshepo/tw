namespace USP.Core.Models.Configuration;

/// <summary>
/// RabbitMQ message broker configuration settings
/// </summary>
public class RabbitMqSettings
{
    /// <summary>
    /// RabbitMQ server hostname
    /// </summary>
    public string HostName { get; set; } = string.Empty;

    /// <summary>
    /// RabbitMQ server port
    /// </summary>
    public int Port { get; set; } = 5672;

    /// <summary>
    /// RabbitMQ username
    /// </summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// RabbitMQ password (loaded from secure configuration)
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// RabbitMQ virtual host
    /// </summary>
    public string VirtualHost { get; set; } = "/";

    /// <summary>
    /// Enable SSL/TLS for RabbitMQ connection
    /// </summary>
    public bool UseSsl { get; set; } = false;

    /// <summary>
    /// Connection timeout in seconds
    /// </summary>
    public int ConnectionTimeout { get; set; } = 30;

    /// <summary>
    /// Heartbeat interval in seconds
    /// </summary>
    public ushort HeartbeatInterval { get; set; } = 60;

    /// <summary>
    /// Automatic connection recovery
    /// </summary>
    public bool AutomaticRecoveryEnabled { get; set; } = true;

    /// <summary>
    /// Network recovery interval in seconds
    /// </summary>
    public int NetworkRecoveryInterval { get; set; } = 5;

    /// <summary>
    /// Validates RabbitMQ configuration settings
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when validation fails</exception>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(HostName))
        {
            throw new InvalidOperationException(
                "RabbitMQ hostname is required. Set RabbitMQ:HostName in configuration.");
        }

        if (Port <= 0 || Port > 65535)
        {
            throw new InvalidOperationException(
                $"RabbitMQ port must be between 1 and 65535. Current value: {Port}");
        }

        if (string.IsNullOrWhiteSpace(UserName))
        {
            throw new InvalidOperationException(
                "RabbitMQ username is required. Set RabbitMQ:UserName in configuration.");
        }

        if (string.IsNullOrWhiteSpace(Password))
        {
            throw new InvalidOperationException(
                "RabbitMQ password is required. Set RabbitMQ:Password in User Secrets (development) or environment variable (production). " +
                "For development: dotnet user-secrets set \"RabbitMQ:Password\" \"your-password\"");
        }

        if (UserName == "guest" && Password == "guest")
        {
            throw new InvalidOperationException(
                "Default RabbitMQ credentials (guest/guest) are not allowed in production. " +
                "Create a dedicated user with appropriate permissions.");
        }

        if (string.IsNullOrWhiteSpace(VirtualHost))
        {
            throw new InvalidOperationException("RabbitMQ virtual host cannot be empty");
        }

        if (ConnectionTimeout <= 0)
        {
            throw new InvalidOperationException(
                $"RabbitMQ connection timeout must be positive. Current value: {ConnectionTimeout} seconds");
        }

        if (HeartbeatInterval == 0)
        {
            throw new InvalidOperationException(
                "RabbitMQ heartbeat interval cannot be zero. Recommended: 60 seconds");
        }

        if (NetworkRecoveryInterval <= 0)
        {
            throw new InvalidOperationException(
                $"RabbitMQ network recovery interval must be positive. Current value: {NetworkRecoveryInterval} seconds");
        }
    }
}
