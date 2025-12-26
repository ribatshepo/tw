using Amazon;
using Amazon.Runtime;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using USP.Core.Models.DTOs.CloudSync;
using USP.Core.Models.Entities;

namespace USP.Infrastructure.Services.CloudSync;

/// <summary>
/// AWS Secrets Manager synchronization implementation
/// </summary>
public class AwsSecretsManagerSync : ICloudSecretSync
{
    private readonly IAmazonSecretsManager _client;
    private readonly ILogger<AwsSecretsManagerSync> _logger;
    private readonly AsyncRetryPolicy _retryPolicy;

    public string ProviderName => "AWS";

    public AwsSecretsManagerSync(
        CloudSyncConfiguration configuration,
        ILogger<AwsSecretsManagerSync> logger)
    {
        _logger = logger;

        // Configure AWS credentials
        AWSCredentials credentials;
        if (!string.IsNullOrEmpty(configuration.AwsIamRoleArn))
        {
            // Use IAM role (for EC2/ECS/Lambda)
            credentials = new InstanceProfileAWSCredentials();
            _logger.LogInformation("Using IAM role authentication for AWS Secrets Manager");
        }
        else if (!string.IsNullOrEmpty(configuration.AwsAccessKeyId))
        {
            // Use access key credentials
            credentials = new BasicAWSCredentials(
                configuration.AwsAccessKeyId,
                configuration.AwsSecretAccessKey);
            _logger.LogInformation("Using access key authentication for AWS Secrets Manager");
        }
        else
        {
            throw new ArgumentException("AWS credentials not configured");
        }

        // Create AWS Secrets Manager client
        var region = RegionEndpoint.GetBySystemName(configuration.AwsRegion ?? "us-east-1");
        _client = new AmazonSecretsManagerClient(credentials, region);

        // Configure retry policy with exponential backoff
        _retryPolicy = Policy
            .Handle<Exception>(ex => !(ex is ResourceNotFoundException))
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (exception, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning(
                        exception,
                        "AWS Secrets Manager operation failed. Retry {RetryCount} after {Delay}s",
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

            // Check if secret exists
            var exists = await SecretExistsAsync(secretName);

            if (exists)
            {
                // Update existing secret
                var updateRequest = new PutSecretValueRequest
                {
                    SecretId = secretName,
                    SecretString = value
                };

                var updateResponse = await _retryPolicy.ExecuteAsync(
                    async () => await _client.PutSecretValueAsync(updateRequest));

                _logger.LogInformation(
                    "Updated secret {SecretPath} in AWS Secrets Manager. Version: {Version}",
                    path,
                    updateResponse.VersionId);

                return SyncResult.SuccessResult(
                    updateResponse.ARN,
                    $"Updated version {updateResponse.VersionId}");
            }
            else
            {
                // Create new secret
                var createRequest = new CreateSecretRequest
                {
                    Name = secretName,
                    SecretString = value,
                    Description = $"Synced from USP: {path}",
                    Tags = BuildTags(metadata)
                };

                var createResponse = await _retryPolicy.ExecuteAsync(
                    async () => await _client.CreateSecretAsync(createRequest));

                _logger.LogInformation(
                    "Created secret {SecretPath} in AWS Secrets Manager. ARN: {Arn}",
                    path,
                    createResponse.ARN);

                return SyncResult.SuccessResult(
                    createResponse.ARN,
                    "Created successfully");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to push secret {SecretPath} to AWS Secrets Manager",
                path);

            return SyncResult.FailureResult($"AWS Secrets Manager error: {ex.Message}");
        }
    }

    public async Task<SyncResult> PullSecretAsync(string path)
    {
        try
        {
            var secretName = NormalizePath(path);

            var request = new GetSecretValueRequest
            {
                SecretId = secretName
            };

            var response = await _retryPolicy.ExecuteAsync(
                async () => await _client.GetSecretValueAsync(request));

            _logger.LogInformation(
                "Retrieved secret {SecretPath} from AWS Secrets Manager. Version: {Version}",
                path,
                response.VersionId);

            return new SyncResult
            {
                Success = true,
                Message = response.SecretString,
                ExternalId = response.ARN,
                Timestamp = DateTime.UtcNow
            };
        }
        catch (ResourceNotFoundException)
        {
            _logger.LogWarning("Secret {SecretPath} not found in AWS Secrets Manager", path);
            return SyncResult.FailureResult("Secret not found");
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to pull secret {SecretPath} from AWS Secrets Manager",
                path);

            return SyncResult.FailureResult($"AWS Secrets Manager error: {ex.Message}");
        }
    }

    public async Task<SyncResult> DeleteSecretAsync(string path)
    {
        try
        {
            var secretName = NormalizePath(path);

            var request = new DeleteSecretRequest
            {
                SecretId = secretName,
                ForceDeleteWithoutRecovery = false, // Allow 30-day recovery window
                RecoveryWindowInDays = 30
            };

            var response = await _retryPolicy.ExecuteAsync(
                async () => await _client.DeleteSecretAsync(request));

            _logger.LogInformation(
                "Scheduled deletion of secret {SecretPath} in AWS Secrets Manager. Deletion date: {DeletionDate}",
                path,
                response.DeletionDate);

            return SyncResult.SuccessResult(
                response.ARN,
                $"Scheduled for deletion on {response.DeletionDate}");
        }
        catch (ResourceNotFoundException)
        {
            _logger.LogWarning("Secret {SecretPath} not found in AWS Secrets Manager", path);
            return SyncResult.SuccessResult(null, "Secret already deleted");
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to delete secret {SecretPath} from AWS Secrets Manager",
                path);

            return SyncResult.FailureResult($"AWS Secrets Manager error: {ex.Message}");
        }
    }

    public async Task<List<string>> ListSecretsAsync(string? prefix = null)
    {
        var secrets = new List<string>();

        try
        {
            string? nextToken = null;

            do
            {
                var request = new ListSecretsRequest
                {
                    MaxResults = 100,
                    NextToken = nextToken
                };

                var response = await _retryPolicy.ExecuteAsync(
                    async () => await _client.ListSecretsAsync(request));

                foreach (var secret in response.SecretList)
                {
                    if (string.IsNullOrEmpty(prefix) || secret.Name.StartsWith(prefix))
                    {
                        secrets.Add(secret.Name);
                    }
                }

                nextToken = response.NextToken;

            } while (!string.IsNullOrEmpty(nextToken));

            _logger.LogInformation(
                "Listed {Count} secrets from AWS Secrets Manager",
                secrets.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to list secrets from AWS Secrets Manager");
        }

        return secrets;
    }

    public async Task<Dictionary<string, object>?> GetSecretMetadataAsync(string path)
    {
        try
        {
            var secretName = NormalizePath(path);

            var request = new DescribeSecretRequest
            {
                SecretId = secretName
            };

            var response = await _retryPolicy.ExecuteAsync(
                async () => await _client.DescribeSecretAsync(request));

            var metadata = new Dictionary<string, object>
            {
                ["ARN"] = response.ARN,
                ["Name"] = response.Name,
                ["Description"] = response.Description ?? string.Empty,
                ["CreatedDate"] = response.CreatedDate,
                ["LastAccessedDate"] = response.LastAccessedDate,
                ["LastChangedDate"] = response.LastChangedDate,
                ["LastRotatedDate"] = response.LastRotatedDate,
                ["VersionIdsToStages"] = response.VersionIdsToStages
            };

            if (response.Tags != null && response.Tags.Any())
            {
                metadata["Tags"] = response.Tags.ToDictionary(t => t.Key, t => t.Value);
            }

            return metadata;
        }
        catch (ResourceNotFoundException)
        {
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to get metadata for secret {SecretPath} from AWS Secrets Manager",
                path);

            return null;
        }
    }

    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            // Try to list secrets to test connectivity
            var request = new ListSecretsRequest
            {
                MaxResults = 1
            };

            await _client.ListSecretsAsync(request);

            _logger.LogInformation("AWS Secrets Manager connection test successful");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AWS Secrets Manager connection test failed");
            return false;
        }
    }

    private async Task<bool> SecretExistsAsync(string secretName)
    {
        try
        {
            var request = new DescribeSecretRequest
            {
                SecretId = secretName
            };

            await _client.DescribeSecretAsync(request);
            return true;
        }
        catch (ResourceNotFoundException)
        {
            return false;
        }
    }

    private string NormalizePath(string path)
    {
        // AWS Secrets Manager requires alphanumeric and /_+=.@-
        // Replace / with - to maintain hierarchy
        return path.Replace("/", "-").Replace("--", "-");
    }

    private List<Tag> BuildTags(Dictionary<string, string>? metadata)
    {
        var tags = new List<Tag>
        {
            new() { Key = "SyncedFrom", Value = "USP" },
            new() { Key = "SyncTimestamp", Value = DateTime.UtcNow.ToString("O") }
        };

        if (metadata != null)
        {
            foreach (var kvp in metadata)
            {
                tags.Add(new Tag { Key = kvp.Key, Value = kvp.Value });
            }
        }

        return tags;
    }
}
