using USP.Core.Domain.Entities.Secrets;
using USP.Core.Domain.Enums;

namespace USP.Core.Interfaces.Services.Secrets;

/// <summary>
/// Service for encryption/decryption operations (Transit engine pattern)
/// </summary>
public interface IEncryptionService
{
    /// <summary>
    /// Create a new encryption key
    /// </summary>
    Task<EncryptionKey> CreateKeyAsync(
        string name,
        EncryptionAlgorithm algorithm = EncryptionAlgorithm.AES256GCM,
        bool exportable = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Read encryption key metadata
    /// </summary>
    Task<EncryptionKey?> ReadKeyAsync(
        string name,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// List all encryption keys
    /// </summary>
    Task<List<string>> ListKeysAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete an encryption key
    /// </summary>
    Task DeleteKeyAsync(
        string name,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Rotate an encryption key (creates new version)
    /// </summary>
    Task<EncryptionKey> RotateKeyAsync(
        string name,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Encrypt plaintext using the specified key
    /// </summary>
    Task<string> EncryptAsync(
        string keyName,
        string plaintext,
        string? context = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Decrypt ciphertext using the specified key
    /// </summary>
    Task<string> DecryptAsync(
        string keyName,
        string ciphertext,
        string? context = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Re-encrypt ciphertext with latest key version
    /// </summary>
    Task<string> RewrapAsync(
        string keyName,
        string ciphertext,
        string? context = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate random bytes
    /// </summary>
    Task<byte[]> GenerateRandomBytesAsync(
        int length,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Hash data using the specified algorithm
    /// </summary>
    Task<string> HashAsync(
        string data,
        string algorithm = "sha2-256",
        CancellationToken cancellationToken = default);
}
