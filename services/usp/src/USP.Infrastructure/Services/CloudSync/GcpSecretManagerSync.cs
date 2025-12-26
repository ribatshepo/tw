using Google.Api.Gax.ResourceNames;
using Google.Cloud.SecretManager.V1;
using Google.Protobuf;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using USP.Core.Models.DTOs.CloudSync;
using USP.Core.Models.Entities;

namespace USP.Infrastructure.Services.CloudSync;

/// <summary>
/// Google Cloud Secret Manager synchronization implementation
/// </summary>
public class GcpSecretManagerSync : ICloudSecretSync
{
    private readonly SecretManagerServiceClient _client;
    private readonly ILogger<GcpSecretManagerSync> _logger;
    private readonly AsyncRetryPolicy _retryPolicy;
    private readonly string _projectId;

    public string ProviderName => "GCP";

    public GcpSecretManagerSync(
        CloudSyncConfiguration configuration,
        ILogger<GcpSecretManagerSync> logger)
    {
        _logger = logger;

        if (string.IsNullOrEmpty(configuration.GcpProjectId))
        {
            throw new ArgumentException("GCP project ID not configured");
        }

        _projectId = configuration.GcpProjectId;

        // Configure GCP credentials
        if (!string.IsNullOrEmpty(configuration.GcpServiceAccountJson))
        {
            // Use service account JSON
            var credential = Google.Apis.Auth.OAuth2.GoogleCredential
                .FromJson(configuration.GcpServiceAccountJson);

            var builder = new SecretManagerServiceClientBuilder
            {
                Credential = credential
            };

            _client = builder.Build();
            _logger.LogInformation("Using service account authentication for GCP Secret Manager");
        }
        else
        {
            // Use default credentials (for GCE/Cloud Run)
            _client = SecretManagerServiceClient.Create();
            _logger.LogInformation("Using default credentials for GCP Secret Manager");
        }

        // Configure retry policy with exponential backoff
        _retryPolicy = Policy
            .Handle<RpcException>(ex => ex.StatusCode != StatusCode.NotFound)
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (exception, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning(
                        exception,
                        "GCP Secret Manager operation failed. Retry {RetryCount} after {Delay}s",
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
            var secretId = NormalizePath(path);
            var secretName = new SecretName(_projectId, secretId);

            // Check if secret exists
            var exists = await SecretExistsAsync(secretName);

            if (!exists)
            {
                // Create secret
                var parent = new ProjectName(_projectId);
                var secret = new Google.Cloud.SecretManager.V1.Secret
                {
                    Replication = new Replication
                    {
                        Automatic = new Replication.Types.Automatic()
                    },
                    Labels =
                    {
                        { "synced_from", "usp" },
                        { "sync_timestamp", DateTime.UtcNow.Ticks.ToString() }
                    }
                };

                if (metadata != null)
                {
                    foreach (var kvp in metadata)
                    {
                        var key = NormalizeLabel(kvp.Key);
                        secret.Labels.TryAdd(key, kvp.Value);
                    }
                }

                await _retryPolicy.ExecuteAsync(
                    async () => await _client.CreateSecretAsync(parent, secretId, secret));

                _logger.LogInformation(
                    "Created secret {SecretPath} in GCP Secret Manager",
                    path);
            }

            // Add secret version
            var secretVersion = new SecretPayload
            {
                Data = ByteString.CopyFromUtf8(value)
            };

            var versionResponse = await _retryPolicy.ExecuteAsync(
                async () => await _client.AddSecretVersionAsync(secretName, secretVersion));

            _logger.LogInformation(
                "Added version to secret {SecretPath} in GCP Secret Manager. Version: {Version}",
                path,
                versionResponse.Name);

            return SyncResult.SuccessResult(
                versionResponse.Name,
                $"Version {versionResponse.Name}");
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to push secret {SecretPath} to GCP Secret Manager",
                path);

            return SyncResult.FailureResult($"GCP Secret Manager error: {ex.Message}");
        }
    }

    public async Task<SyncResult> PullSecretAsync(string path)
    {
        try
        {
            var secretId = NormalizePath(path);
            var secretVersionName = new SecretVersionName(_projectId, secretId, "latest");

            var response = await _retryPolicy.ExecuteAsync(
                async () => await _client.AccessSecretVersionAsync(secretVersionName));

            var secretValue = response.Payload.Data.ToStringUtf8();

            _logger.LogInformation(
                "Retrieved secret {SecretPath} from GCP Secret Manager. Version: {Version}",
                path,
                response.Name);

            return new SyncResult
            {
                Success = true,
                Message = secretValue,
                ExternalId = response.Name,
                Timestamp = DateTime.UtcNow
            };
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            _logger.LogWarning("Secret {SecretPath} not found in GCP Secret Manager", path);
            return SyncResult.FailureResult("Secret not found");
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to pull secret {SecretPath} from GCP Secret Manager",
                path);

            return SyncResult.FailureResult($"GCP Secret Manager error: {ex.Message}");
        }
    }

    public async Task<SyncResult> DeleteSecretAsync(string path)
    {
        try
        {
            var secretId = NormalizePath(path);
            var secretName = new SecretName(_projectId, secretId);

            await _retryPolicy.ExecuteAsync(
                async () => await _client.DeleteSecretAsync(secretName));

            _logger.LogInformation(
                "Deleted secret {SecretPath} from GCP Secret Manager",
                path);

            return SyncResult.SuccessResult(secretName.ToString(), "Deleted successfully");
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            _logger.LogWarning("Secret {SecretPath} not found in GCP Secret Manager", path);
            return SyncResult.SuccessResult(null, "Secret already deleted");
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to delete secret {SecretPath} from GCP Secret Manager",
                path);

            return SyncResult.FailureResult($"GCP Secret Manager error: {ex.Message}");
        }
    }

    public async Task<List<string>> ListSecretsAsync(string? prefix = null)
    {
        var secrets = new List<string>();

        try
        {
            var parent = new ProjectName(_projectId);

            var response = _client.ListSecrets(parent);

            foreach (var secret in response)
            {
                var secretId = ExtractSecretId(secret.Name);

                if (string.IsNullOrEmpty(prefix) || secretId.StartsWith(prefix))
                {
                    secrets.Add(secretId);
                }
            }

            _logger.LogInformation(
                "Listed {Count} secrets from GCP Secret Manager",
                secrets.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to list secrets from GCP Secret Manager");
        }

        return secrets;
    }

    public async Task<Dictionary<string, object>?> GetSecretMetadataAsync(string path)
    {
        try
        {
            var secretId = NormalizePath(path);
            var secretName = new SecretName(_projectId, secretId);

            var response = await _retryPolicy.ExecuteAsync(
                async () => await _client.GetSecretAsync(secretName));

            var metadata = new Dictionary<string, object>
            {
                ["Name"] = response.Name,
                ["CreateTime"] = response.CreateTime.ToDateTime(),
                ["Replication"] = response.Replication.ToString(),
                ["Labels"] = response.Labels.ToDictionary(l => l.Key, l => l.Value)
            };

            if (response.ExpireTime != null)
            {
                metadata["ExpireTime"] = response.ExpireTime.ToDateTime();
            }

            return metadata;
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to get metadata for secret {SecretPath} from GCP Secret Manager",
                path);

            return null;
        }
    }

    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            // Try to list secrets to test connectivity
            var parent = new ProjectName(_projectId);
            var response = _client.ListSecrets(parent, pageSize: 1);

            // Enumerate first item
            using var enumerator = response.GetEnumerator();
            enumerator.MoveNext();

            _logger.LogInformation("GCP Secret Manager connection test successful");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GCP Secret Manager connection test failed");
            return false;
        }
    }

    private async Task<bool> SecretExistsAsync(SecretName secretName)
    {
        try
        {
            await _client.GetSecretAsync(secretName);
            return true;
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            return false;
        }
    }

    private string NormalizePath(string path)
    {
        // GCP Secret Manager requires lowercase alphanumeric and -_
        return path
            .Replace("/", "-")
            .Replace(".", "_")
            .Replace("--", "-")
            .ToLowerInvariant()
            .Trim('-');
    }

    private string NormalizeLabel(string label)
    {
        // GCP labels require lowercase alphanumeric and -_
        return label
            .Replace(".", "_")
            .Replace("/", "_")
            .ToLowerInvariant();
    }

    private string ExtractSecretId(string fullName)
    {
        // Extract secret ID from full name: projects/{project}/secrets/{secretId}
        var parts = fullName.Split('/');
        return parts.Length >= 4 ? parts[3] : fullName;
    }
}
