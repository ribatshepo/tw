using Microsoft.Extensions.Logging;
using Npgsql;
using USP.Core.Services.PAM;

namespace USP.Infrastructure.Services.PAM.Connectors;

/// <summary>
/// PostgreSQL password rotation connector
/// </summary>
public class PostgreSqlConnector : BaseConnector
{
    private readonly ILogger<PostgreSqlConnector> _logger;

    public override string Platform => "PostgreSQL";

    public PostgreSqlConnector(ILogger<PostgreSqlConnector> logger)
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

        try
        {
            // Build connection string with current credentials
            var connectionString = $"Host={hostAddress};Port={port ?? 5432};Database={databaseName ?? "postgres"};Username={username};Password={currentPassword};";

            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();

            // Execute ALTER USER command to change password
            var escapedNewPassword = newPassword.Replace("'", "''");
            var sql = $"ALTER USER \"{username}\" WITH PASSWORD '{escapedNewPassword}';";

            await using var command = new NpgsqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync();

            result.Success = true;
            result.Details = $"Password rotated successfully for user {username} on {hostAddress}";

            _logger.LogInformation(
                "Password rotated successfully for PostgreSQL user {Username} on {Host}",
                username,
                hostAddress);
        }
        catch (Exception ex)
        {
            result.ErrorMessage = ex.Message;
            result.Details = $"Failed to rotate password: {ex.Message}";

            _logger.LogError(ex,
                "Failed to rotate password for PostgreSQL user {Username} on {Host}",
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
        try
        {
            var connectionString = $"Host={hostAddress};Port={port ?? 5432};Database={databaseName ?? "postgres"};Username={username};Password={password};Timeout=10;";

            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();

            // Execute a simple query to verify connection
            await using var command = new NpgsqlCommand("SELECT 1;", connection);
            await command.ExecuteScalarAsync();

            _logger.LogDebug(
                "Credentials verified successfully for PostgreSQL user {Username} on {Host}",
                username,
                hostAddress);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to verify credentials for PostgreSQL user {Username} on {Host}",
                username,
                hostAddress);

            return false;
        }
    }
}
