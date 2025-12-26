namespace USP.Core.Models.Configuration;

/// <summary>
/// PostgreSQL database connection configuration settings
/// </summary>
public class DatabaseSettings
{
    /// <summary>
    /// Database host address
    /// </summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>
    /// Database port number
    /// </summary>
    public int Port { get; set; } = 5432;

    /// <summary>
    /// Database name
    /// </summary>
    public string Database { get; set; } = string.Empty;

    /// <summary>
    /// Database username
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Database password (loaded from secure configuration)
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Include error details in connection (development only)
    /// </summary>
    public bool IncludeErrorDetail { get; set; } = false;

    /// <summary>
    /// Connection timeout in seconds
    /// </summary>
    public int Timeout { get; set; } = 30;

    /// <summary>
    /// Minimum pool size
    /// </summary>
    public int MinPoolSize { get; set; } = 1;

    /// <summary>
    /// Maximum pool size
    /// </summary>
    public int MaxPoolSize { get; set; } = 100;

    /// <summary>
    /// Builds a PostgreSQL connection string from configured settings
    /// </summary>
    /// <returns>PostgreSQL connection string</returns>
    /// <exception cref="InvalidOperationException">Thrown when required settings are missing</exception>
    public string BuildConnectionString()
    {
        if (string.IsNullOrWhiteSpace(Host))
        {
            throw new InvalidOperationException("Database host is required. Set Database:Host in configuration.");
        }

        if (string.IsNullOrWhiteSpace(Database))
        {
            throw new InvalidOperationException("Database name is required. Set Database:Database in configuration.");
        }

        if (string.IsNullOrWhiteSpace(Username))
        {
            throw new InvalidOperationException("Database username is required. Set Database:Username in configuration.");
        }

        if (string.IsNullOrWhiteSpace(Password))
        {
            throw new InvalidOperationException(
                "Database password is required. Set Database:Password in User Secrets (development) or environment variable (production). " +
                "For development: dotnet user-secrets set \"Database:Password\" \"your-password\"");
        }

        var connectionStringBuilder = new System.Text.StringBuilder();
        connectionStringBuilder.Append($"Host={Host};");
        connectionStringBuilder.Append($"Port={Port};");
        connectionStringBuilder.Append($"Database={Database};");
        connectionStringBuilder.Append($"Username={Username};");
        connectionStringBuilder.Append($"Password={Password};");
        connectionStringBuilder.Append($"Timeout={Timeout};");
        connectionStringBuilder.Append($"MinPoolSize={MinPoolSize};");
        connectionStringBuilder.Append($"MaxPoolSize={MaxPoolSize};");

        if (IncludeErrorDetail)
        {
            connectionStringBuilder.Append("Include Error Detail=true;");
        }

        return connectionStringBuilder.ToString();
    }

    /// <summary>
    /// Validates database settings
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when validation fails</exception>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Host))
        {
            throw new InvalidOperationException("Database host cannot be empty");
        }

        if (Port <= 0 || Port > 65535)
        {
            throw new InvalidOperationException("Database port must be between 1 and 65535");
        }

        if (string.IsNullOrWhiteSpace(Database))
        {
            throw new InvalidOperationException("Database name cannot be empty");
        }

        if (string.IsNullOrWhiteSpace(Username))
        {
            throw new InvalidOperationException("Database username cannot be empty");
        }

        if (string.IsNullOrWhiteSpace(Password))
        {
            throw new InvalidOperationException("Database password cannot be empty");
        }

        if (Timeout < 0)
        {
            throw new InvalidOperationException("Database timeout cannot be negative");
        }

        if (MinPoolSize < 0)
        {
            throw new InvalidOperationException("Minimum pool size cannot be negative");
        }

        if (MaxPoolSize < MinPoolSize)
        {
            throw new InvalidOperationException("Maximum pool size must be greater than or equal to minimum pool size");
        }
    }
}
