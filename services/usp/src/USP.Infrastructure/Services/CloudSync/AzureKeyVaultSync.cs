using Azure;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using USP.Core.Models.DTOs.CloudSync;
using USP.Core.Models.Entities;

namespace USP.Infrastructure.Services.CloudSync;

/// <summary>
/// Azure Key Vault synchronization implementation
/// </summary>
public class AzureKeyVaultSync : ICloudSecretSync
{
    private readonly SecretClient _client;
    private readonly ILogger<AzureKeyVaultSync> _logger;
    private readonly AsyncRetryPolicy _retryPolicy;

    public string ProviderName => "Azure";

    public AzureKeyVaultSync(
        CloudSyncConfiguration configuration,
        ILogger<AzureKeyVaultSync> logger)
    {
        _logger = logger;

        if (string.IsNullOrEmpty(configuration.AzureKeyVaultUri))
        {
            throw new ArgumentException("Azure Key Vault URI not configured");
        }

        // Configure Azure credentials
        Azure.Core.TokenCredential credential;

        if (!string.IsNullOrEmpty(configuration.AzureClientId))
        {
            // Use service principal
            credential = new ClientSecretCredential(
                configuration.AzureTenantId,
                configuration.AzureClientId,
                configuration.AzureClientSecret);

            _logger.LogInformation("Using service principal authentication for Azure Key Vault");
        }
        else
        {
            // Use managed identity (for Azure VMs/App Service)
            credential = new DefaultAzureCredential();
            _logger.LogInformation("Using managed identity authentication for Azure Key Vault");
        }

        // Create Azure Key Vault client
        _client = new SecretClient(new Uri(configuration.AzureKeyVaultUri), credential);

        // Configure retry policy with exponential backoff
        _retryPolicy = Policy
            .Handle<RequestFailedException>(ex => ex.Status != 404)
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (exception, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning(
                        exception,
                        "Azure Key Vault operation failed. Retry {RetryCount} after {Delay}s",
                        retryCount,
                        timeSpan.TotalSeconds);
                });
    }

    public async Task<SyncResult> PushSecretAsync(
        string path,
        string value,
        Dictionary<string, string>? metadata = null)
    {
        try
        {
            var secretName = NormalizePath(path);

            var secret = new KeyVaultSecret(secretName, value);

            // Add tags for tracking
            secret.Properties.Tags.Add("SyncedFrom", "USP");
            secret.Properties.Tags.Add("SyncTimestamp", DateTime.UtcNow.ToString("O"));

            if (metadata != null)
            {
                foreach (var kvp in metadata)
                {
                    secret.Properties.Tags.TryAdd(kvp.Key, kvp.Value);
                }
            }

            var response = await _retryPolicy.ExecuteAsync(
                async () => await _client.SetSecretAsync(secret));

            _logger.LogInformation(
                "Pushed secret {SecretPath} to Azure Key Vault. Version: {Version}",
                path,
                response.Value.Properties.Version);

            return SyncResult.SuccessResult(
                response.Value.Id.ToString(),
                $"Version {response.Value.Properties.Version}");
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to push secret {SecretPath} to Azure Key Vault",
                path);

            return SyncResult.FailureResult($"Azure Key Vault error: {ex.Message}");
        }
    }

    public async Task<SyncResult> PullSecretAsync(string path)
    {
        try
        {
            var secretName = NormalizePath(path);

            var response = await _retryPolicy.ExecuteAsync(
                async () => await _client.GetSecretAsync(secretName));

            _logger.LogInformation(
                "Retrieved secret {SecretPath} from Azure Key Vault. Version: {Version}",
                path,
                response.Value.Properties.Version);

            return new SyncResult
            {
                Success = true,
                Message = response.Value.Value,
                ExternalId = response.Value.Id.ToString(),
                Timestamp = DateTime.UtcNow
            };
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogWarning("Secret {SecretPath} not found in Azure Key Vault", path);
            return SyncResult.FailureResult("Secret not found");
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to pull secret {SecretPath} from Azure Key Vault",
                path);

            return SyncResult.FailureResult($"Azure Key Vault error: {ex.Message}");
        }
    }

    public async Task<SyncResult> DeleteSecretAsync(string path)
    {
        try
        {
            var secretName = NormalizePath(path);

            // Start deletion operation (soft delete by default)
            var operation = await _retryPolicy.ExecuteAsync(
                async () => await _client.StartDeleteSecretAsync(secretName));

            // Wait for deletion to complete
            var deletedSecret = await operation.WaitForCompletionAsync();

            _logger.LogInformation(
                "Deleted secret {SecretPath} from Azure Key Vault. Recoverable until: {RecoveryDate}",
                path,
                deletedSecret.Value.ScheduledPurgeDate);

            return SyncResult.SuccessResult(
                deletedSecret.Value.Id.ToString(),
                $"Deleted (recoverable until {deletedSecret.Value.ScheduledPurgeDate})");
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogWarning("Secret {SecretPath} not found in Azure Key Vault", path);
            return SyncResult.SuccessResult(null, "Secret already deleted");
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to delete secret {SecretPath} from Azure Key Vault",
                path);

            return SyncResult.FailureResult($"Azure Key Vault error: {ex.Message}");
        }
    }

    public async Task<List<string>> ListSecretsAsync(string? prefix = null)
    {
        var secrets = new List<string>();

        try
        {
            await foreach (var secretProperties in _client.GetPropertiesOfSecretsAsync())
            {
                var secretName = secretProperties.Name;

                if (string.IsNullOrEmpty(prefix) || secretName.StartsWith(prefix))
                {
                    secrets.Add(secretName);
                }
            }

            _logger.LogInformation(
                "Listed {Count} secrets from Azure Key Vault",
                secrets.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to list secrets from Azure Key Vault");
        }

        return secrets;
    }

    public async Task<Dictionary<string, object>?> GetSecretMetadataAsync(string path)
    {
        try
        {
            var secretName = NormalizePath(path);

            var response = await _retryPolicy.ExecuteAsync(
                async () => await _client.GetSecretAsync(secretName));

            var properties = response.Value.Properties;

            var metadata = new Dictionary<string, object>
            {
                ["Id"] = response.Value.Id.ToString(),
                ["Name"] = properties.Name,
                ["Version"] = properties.Version ?? string.Empty,
                ["Enabled"] = properties.Enabled ?? false,
                ["CreatedOn"] = properties.CreatedOn ?? DateTimeOffset.MinValue,
                ["UpdatedOn"] = properties.UpdatedOn ?? DateTimeOffset.MinValue,
                ["RecoveryLevel"] = properties.RecoveryLevel ?? string.Empty,
                ["Tags"] = properties.Tags.ToDictionary(t => t.Key, t => t.Value)
            };

            if (properties.ExpiresOn.HasValue)
            {
                metadata["ExpiresOn"] = properties.ExpiresOn.Value;
            }

            if (properties.NotBefore.HasValue)
            {
                metadata["NotBefore"] = properties.NotBefore.Value;
            }

            return metadata;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to get metadata for secret {SecretPath} from Azure Key Vault",
                path);

            return null;
        }
    }

    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            // Try to list first secret to test connectivity
            await foreach (var _ in _client.GetPropertiesOfSecretsAsync())
            {
                break; // Just need to get first item
            }

            _logger.LogInformation("Azure Key Vault connection test successful");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Azure Key Vault connection test failed");
            return false;
        }
    }

    private string NormalizePath(string path)
    {
        // Azure Key Vault requires alphanumeric and -
        // Replace / with - to maintain hierarchy
        return path
            .Replace("/", "-")
            .Replace("_", "-")
            .Replace(".", "-")
            .Replace("--", "-")
            .Trim('-');
    }
}
