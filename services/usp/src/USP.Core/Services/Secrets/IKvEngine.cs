using USP.Core.Models.DTOs.Secrets;

namespace USP.Core.Services.Secrets;

/// <summary>
/// Vault KV v2 compatible secret engine
/// Provides versioned key-value secret storage with soft delete and recovery
/// </summary>
public interface IKvEngine
{
    /// <summary>
    /// Create or update a secret at the specified path
    /// </summary>
    Task<CreateSecretResponse> CreateSecretAsync(string path, CreateSecretRequest request, Guid userId);

    /// <summary>
    /// Read a secret (latest version or specific version)
    /// </summary>
    Task<ReadSecretResponse?> ReadSecretAsync(string path, int? version, Guid userId);

    /// <summary>
    /// Soft delete specific versions of a secret
    /// </summary>
    Task DeleteSecretVersionsAsync(string path, DeleteSecretVersionsRequest request, Guid userId);

    /// <summary>
    /// Undelete soft-deleted versions of a secret
    /// </summary>
    Task UndeleteSecretVersionsAsync(string path, UndeleteSecretVersionsRequest request, Guid userId);

    /// <summary>
    /// Permanently destroy specific versions of a secret
    /// </summary>
    Task DestroySecretVersionsAsync(string path, DestroySecretVersionsRequest request, Guid userId);

    /// <summary>
    /// Read metadata for a secret path
    /// </summary>
    Task<SecretMetadata?> ReadSecretMetadataAsync(string path, Guid userId);

    /// <summary>
    /// Update metadata for a secret path
    /// </summary>
    Task UpdateSecretMetadataAsync(string path, UpdateSecretMetadataRequest request, Guid userId);

    /// <summary>
    /// Delete all versions and metadata for a secret path
    /// </summary>
    Task DeleteSecretMetadataAsync(string path, Guid userId);

    /// <summary>
    /// List secrets at a given path
    /// </summary>
    Task<ListSecretsResponse> ListSecretsAsync(string path, Guid userId);
}
