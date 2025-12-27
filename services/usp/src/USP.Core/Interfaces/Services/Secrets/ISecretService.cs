using USP.Core.Domain.Entities.Secrets;

namespace USP.Core.Interfaces.Services.Secrets;

/// <summary>
/// Service for managing secrets with versioning (Vault KV v2 compatible)
/// </summary>
public interface ISecretService
{
    /// <summary>
    /// Create or update a secret (creates new version)
    /// </summary>
    Task<SecretVersion> WriteSecretAsync(
        string path,
        Dictionary<string, string> data,
        int? cas = null,
        string? userId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Read the latest version of a secret
    /// </summary>
    Task<Dictionary<string, string>?> ReadSecretAsync(
        string path,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Read a specific version of a secret
    /// </summary>
    Task<Dictionary<string, string>?> ReadSecretVersionAsync(
        string path,
        int version,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// List all secrets at a path
    /// </summary>
    Task<List<string>> ListSecretsAsync(
        string path,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft delete the latest version of a secret
    /// </summary>
    Task DeleteSecretAsync(
        string path,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft delete specific versions of a secret
    /// </summary>
    Task DeleteSecretVersionsAsync(
        string path,
        List<int> versions,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Undelete specific versions of a secret
    /// </summary>
    Task UndeleteSecretVersionsAsync(
        string path,
        List<int> versions,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Permanently destroy specific versions of a secret (cannot be recovered)
    /// </summary>
    Task DestroySecretVersionsAsync(
        string path,
        List<int> versions,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get metadata about a secret
    /// </summary>
    Task<Secret?> GetSecretMetadataAsync(
        string path,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Update secret metadata
    /// </summary>
    Task UpdateSecretMetadataAsync(
        string path,
        int? maxVersions = null,
        bool? casRequired = null,
        CancellationToken cancellationToken = default);
}
