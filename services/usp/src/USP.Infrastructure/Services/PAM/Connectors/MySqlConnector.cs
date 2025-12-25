using Microsoft.Extensions.Logging;
using MySqlConnector;
using USP.Core.Services.PAM;

namespace USP.Infrastructure.Services.PAM.Connectors;

/// <summary>
/// MySQL password rotation connector
/// </summary>
public class MySqlPasswordConnector : BaseConnector
{
    private readonly ILogger<MySqlPasswordConnector> _logger;

    public override string Platform => "MySQL";

    public MySqlPasswordConnector(ILogger<MySqlPasswordConnector> logger)
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
            var connectionString = $"Server={hostAddress};Port={port ?? 3306};Database={databaseName ?? "mysql"};Uid={username};Pwd={currentPassword};";

            await using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();

            // Execute ALTER USER command to change password
            var escapedNewPassword = newPassword.Replace("'", "''");
            var sql = $"ALTER USER '{username}'@'%' IDENTIFIED BY '{escapedNewPassword}';";

            await using var command = new MySqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync();

            // Flush privileges
            await using var flushCommand = new MySqlCommand("FLUSH PRIVILEGES;", connection);
            await flushCommand.ExecuteNonQueryAsync();

            result.Success = true;
            result.Details = $"Password rotated successfully for user {username} on {hostAddress}";

            _logger.LogInformation(
                "Password rotated successfully for MySQL user {Username} on {Host}",
                username,
                hostAddress);
        }
        catch (Exception ex)
        {
            result.ErrorMessage = ex.Message;
            result.Details = $"Failed to rotate password: {ex.Message}";

            _logger.LogError(ex,
                "Failed to rotate password for MySQL user {Username} on {Host}",
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
            var connectionString = $"Server={hostAddress};Port={port ?? 3306};Database={databaseName ?? "mysql"};Uid={username};Pwd={password};ConnectionTimeout=10;";

            await using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();

            // Execute a simple query to verify connection
            await using var command = new MySqlCommand("SELECT 1;", connection);
            await command.ExecuteScalarAsync();

            _logger.LogDebug(
                "Credentials verified successfully for MySQL user {Username} on {Host}",
                username,
                hostAddress);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to verify credentials for MySQL user {Username} on {Host}",
                username,
                hostAddress);

            return false;
        }
    }
}
