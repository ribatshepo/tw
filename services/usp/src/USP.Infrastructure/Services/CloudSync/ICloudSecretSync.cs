using USP.Core.Models.DTOs.CloudSync;

namespace USP.Infrastructure.Services.CloudSync;

/// <summary>
/// Interface for cloud provider secret synchronization
/// </summary>
public interface ICloudSecretSync
{
    /// <summary>
    /// Push a secret to the cloud provider
    /// </summary>
    Task<SyncResult> PushSecretAsync(string path, string value, Dictionary<string, string>? metadata = null);

    /// <summary>
    /// Pull a secret from the cloud provider
    /// </summary>
    Task<SyncResult> PullSecretAsync(string path);

    /// <summary>
    /// Delete a secret from the cloud provider
    /// </summary>
    Task<SyncResult> DeleteSecretAsync(string path);

    /// <summary>
    /// List all secrets from the cloud provider
    /// </summary>
    Task<List<string>> ListSecretsAsync(string? prefix = null);

    /// <summary>
    /// Get secret metadata from cloud provider
    /// </summary>
    Task<Dictionary<string, object>?> GetSecretMetadataAsync(string path);

    /// <summary>
    /// Test connection to cloud provider
    /// </summary>
    Task<bool> TestConnectionAsync();

    /// <summary>
    /// Get the cloud provider name
    /// </summary>
    string ProviderName { get; }
}
