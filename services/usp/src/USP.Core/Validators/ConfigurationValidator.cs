using Microsoft.Extensions.Configuration;
using USP.Core.Models.Configuration;

namespace USP.Core.Validators;

/// <summary>
/// Validates all configuration settings at application startup
/// Ensures fail-fast behavior when required configuration is missing or invalid
/// </summary>
public static class ConfigurationValidator
{
    /// <summary>
    /// Validates all critical configuration sections required for USP operation
    /// </summary>
    /// <param name="configuration">Application configuration</param>
    /// <exception cref="InvalidOperationException">Thrown when any configuration validation fails</exception>
    public static void ValidateConfiguration(IConfiguration configuration)
    {
        var errors = new List<string>();

        try
        {
            ValidateDatabaseConfiguration(configuration);
        }
        catch (Exception ex)
        {
            errors.Add($"Database configuration: {ex.Message}");
        }

        try
        {
            ValidateJwtConfiguration(configuration);
        }
        catch (Exception ex)
        {
            errors.Add($"JWT configuration: {ex.Message}");
        }

        try
        {
            ValidateRedisConfiguration(configuration);
        }
        catch (Exception ex)
        {
            errors.Add($"Redis configuration: {ex.Message}");
        }

        try
        {
            ValidateRabbitMqConfiguration(configuration);
        }
        catch (Exception ex)
        {
            errors.Add($"RabbitMQ configuration: {ex.Message}");
        }

        try
        {
            ValidateEmailConfiguration(configuration);
        }
        catch (Exception ex)
        {
            errors.Add($"Email configuration: {ex.Message}");
        }

        try
        {
            ValidateWebAuthnConfiguration(configuration);
        }
        catch (Exception ex)
        {
            errors.Add($"WebAuthn configuration: {ex.Message}");
        }

        if (errors.Any())
        {
            var errorMessage = "Configuration validation failed:\n" + string.Join("\n", errors.Select(e => $"  - {e}"));
            throw new InvalidOperationException(errorMessage);
        }
    }

    /// <summary>
    /// Validates database configuration settings
    /// </summary>
    private static void ValidateDatabaseConfiguration(IConfiguration configuration)
    {
        var dbSettings = new DatabaseSettings();
        configuration.GetSection("Database").Bind(dbSettings);

        dbSettings.Validate();

        // Test connection string building
        var connectionString = dbSettings.BuildConnectionString();
    }

    /// <summary>
    /// Validates JWT authentication configuration settings
    /// </summary>
    private static void ValidateJwtConfiguration(IConfiguration configuration)
    {
        var jwtSettings = new JwtSettings();
        configuration.GetSection("Jwt").Bind(jwtSettings);

        jwtSettings.Validate();
    }

    /// <summary>
    /// Validates Redis cache configuration settings
    /// </summary>
    private static void ValidateRedisConfiguration(IConfiguration configuration)
    {
        var redisSettings = new RedisSettings();
        configuration.GetSection("Redis").Bind(redisSettings);

        redisSettings.Validate();

        // Test connection string building
        var connectionString = redisSettings.BuildConnectionString();
    }

    /// <summary>
    /// Validates RabbitMQ message broker configuration settings
    /// </summary>
    private static void ValidateRabbitMqConfiguration(IConfiguration configuration)
    {
        var rabbitMqSettings = new RabbitMqSettings();
        configuration.GetSection("RabbitMQ").Bind(rabbitMqSettings);

        rabbitMqSettings.Validate();
    }

    /// <summary>
    /// Validates email service configuration settings
    /// </summary>
    private static void ValidateEmailConfiguration(IConfiguration configuration)
    {
        var emailSettings = new EmailSettings();
        configuration.GetSection("Email").Bind(emailSettings);

        // Email is optional, but if configured, validate it
        if (!string.IsNullOrWhiteSpace(emailSettings.SmtpHost))
        {
            if (string.IsNullOrWhiteSpace(emailSettings.FromEmail))
            {
                throw new InvalidOperationException("Email FromEmail is required when SMTP is configured");
            }

            if (emailSettings.SmtpPort <= 0 || emailSettings.SmtpPort > 65535)
            {
                throw new InvalidOperationException($"Email SMTP port must be between 1 and 65535. Current: {emailSettings.SmtpPort}");
            }
        }
    }

    /// <summary>
    /// Validates WebAuthn/FIDO2 configuration settings
    /// </summary>
    private static void ValidateWebAuthnConfiguration(IConfiguration configuration)
    {
        var webAuthnSettings = new WebAuthnSettings();
        configuration.GetSection("WebAuthn").Bind(webAuthnSettings);

        if (string.IsNullOrWhiteSpace(webAuthnSettings.RelyingPartyId))
        {
            throw new InvalidOperationException("WebAuthn RelyingPartyId is required");
        }

        if (string.IsNullOrWhiteSpace(webAuthnSettings.Origin))
        {
            throw new InvalidOperationException("WebAuthn Origin is required");
        }

        if (webAuthnSettings.TimestampDriftTolerance <= 0)
        {
            throw new InvalidOperationException("WebAuthn TimestampDriftTolerance must be positive");
        }
    }

    /// <summary>
    /// Checks if any configuration value contains prohibited patterns (weak defaults)
    /// </summary>
    /// <param name="configuration">Application configuration</param>
    public static void CheckForProhibitedPatterns(IConfiguration configuration)
    {
        var prohibitedPatterns = new[]
        {
            "changeme",
            "change_me",
            "password123",
            "admin123",
            "default",
            "secret123",
            "your-secret",
            "your-password",
            "development-secret",
            "dev-password"
        };

        var violations = new List<string>();

        // Recursively check all configuration values
        CheckConfigurationSection(configuration, "", prohibitedPatterns, violations);

        if (violations.Any())
        {
            var errorMessage = "Configuration contains prohibited weak default values:\n" +
                string.Join("\n", violations.Select(v => $"  - {v}"));
            throw new InvalidOperationException(errorMessage);
        }
    }

    /// <summary>
    /// Recursively checks configuration section for prohibited patterns
    /// </summary>
    private static void CheckConfigurationSection(
        IConfiguration configuration,
        string prefix,
        string[] prohibitedPatterns,
        List<string> violations)
    {
        foreach (var child in configuration.GetChildren())
        {
            var key = string.IsNullOrEmpty(prefix) ? child.Key : $"{prefix}:{child.Key}";
            var value = child.Value;

            if (!string.IsNullOrEmpty(value))
            {
                // Skip checking certain keys that may legitimately contain these patterns in documentation
                var skipKeys = new[] { "Description", "Comment", "Example" };
                if (skipKeys.Any(sk => key.EndsWith(sk, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                foreach (var pattern in prohibitedPatterns)
                {
                    if (value.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                    {
                        violations.Add($"{key} contains prohibited pattern: '{pattern}'");
                    }
                }
            }

            // Recursively check child sections
            if (child.GetChildren().Any())
            {
                CheckConfigurationSection(child, key, prohibitedPatterns, violations);
            }
        }
    }
}
