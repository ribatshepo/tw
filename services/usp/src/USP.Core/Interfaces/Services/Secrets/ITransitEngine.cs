namespace USP.Core.Interfaces.Services.Secrets;

/// <summary>
/// Provides encryption-as-a-service (Transit Engine) operations.
/// Allows applications to encrypt, decrypt, sign, and verify data without
/// managing cryptographic keys directly.
/// </summary>
public interface ITransitEngine
{
    /// <summary>
    /// Creates a new named encryption key.
    /// </summary>
    /// <param name="name">Unique name for the key</param>
    /// <param name="keyType">Type of key (aes256-gcm96, rsa-2048, ecdsa-p256, etc.)</param>
    /// <param name="derivation">Whether to enable key derivation</param>
    /// <param name="exportable">Whether the key can be exported</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task CreateKeyAsync(
        string name,
        string keyType = "aes256-gcm96",
        bool derivation = false,
        bool exportable = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Encrypts data using the specified key.
    /// </summary>
    /// <param name="keyName">Name of the key to use</param>
    /// <param name="plaintext">Base64-encoded plaintext</param>
    /// <param name="context">Base64-encoded context for key derivation (optional)</param>
    /// <param name="keyVersion">Specific key version to use (optional, uses latest)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Vault-formatted ciphertext</returns>
    Task<TransitEncryptResult> EncryptAsync(
        string keyName,
        string plaintext,
        string? context = null,
        int? keyVersion = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Decrypts data using the specified key.
    /// </summary>
    /// <param name="keyName">Name of the key to use</param>
    /// <param name="ciphertext">Vault-formatted ciphertext</param>
    /// <param name="context">Base64-encoded context for key derivation (optional)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Base64-encoded plaintext</returns>
    Task<TransitDecryptResult> DecryptAsync(
        string keyName,
        string ciphertext,
        string? context = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Signs data using the specified key.
    /// </summary>
    /// <param name="keyName">Name of the signing key</param>
    /// <param name="input">Base64-encoded data to sign</param>
    /// <param name="hashAlgorithm">Hash algorithm (sha2-256, sha2-512, etc.)</param>
    /// <param name="context">Base64-encoded context (optional)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Vault-formatted signature</returns>
    Task<TransitSignResult> SignAsync(
        string keyName,
        string input,
        string hashAlgorithm = "sha2-256",
        string? context = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies a signature.
    /// </summary>
    /// <param name="keyName">Name of the signing key</param>
    /// <param name="input">Base64-encoded data that was signed</param>
    /// <param name="signature">Vault-formatted signature</param>
    /// <param name="hashAlgorithm">Hash algorithm (sha2-256, sha2-512, etc.)</param>
    /// <param name="context">Base64-encoded context (optional)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Whether the signature is valid</returns>
    Task<TransitVerifyResult> VerifyAsync(
        string keyName,
        string input,
        string signature,
        string hashAlgorithm = "sha2-256",
        string? context = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates an HMAC for data.
    /// </summary>
    /// <param name="keyName">Name of the key to use</param>
    /// <param name="input">Base64-encoded data to HMAC</param>
    /// <param name="hashAlgorithm">Hash algorithm (sha2-256, sha2-512, etc.)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Vault-formatted HMAC</returns>
    Task<TransitHmacResult> GenerateHmacAsync(
        string keyName,
        string input,
        string hashAlgorithm = "sha2-256",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Rotates the encryption key to a new version.
    /// Old versions can still decrypt but new encryptions use the new version.
    /// </summary>
    /// <param name="keyName">Name of the key to rotate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task RotateKeyAsync(
        string keyName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Re-encrypts data encrypted with an old key version to use the latest version.
    /// </summary>
    /// <param name="keyName">Name of the key</param>
    /// <param name="ciphertext">Vault-formatted ciphertext</param>
    /// <param name="context">Base64-encoded context (optional)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Ciphertext encrypted with latest key version</returns>
    Task<TransitRewrapResult> RewrapAsync(
        string keyName,
        string ciphertext,
        string? context = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads key information (metadata, versions, etc.).
    /// </summary>
    /// <param name="keyName">Name of the key</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Key information</returns>
    Task<TransitKeyInfo?> ReadKeyAsync(
        string keyName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all transit keys.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of key names</returns>
    Task<List<string>> ListKeysAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a transit key (if deletion is allowed).
    /// </summary>
    /// <param name="keyName">Name of the key to delete</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DeleteKeyAsync(
        string keyName,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of an encryption operation.
/// </summary>
public class TransitEncryptResult
{
    /// <summary>
    /// Vault-formatted ciphertext (vault:v{version}:...)
    /// </summary>
    public required string Ciphertext { get; set; }

    /// <summary>
    /// Key version used for encryption.
    /// </summary>
    public required int KeyVersion { get; set; }
}

/// <summary>
/// Result of a decryption operation.
/// </summary>
public class TransitDecryptResult
{
    /// <summary>
    /// Base64-encoded plaintext.
    /// </summary>
    public required string Plaintext { get; set; }
}

/// <summary>
/// Result of a signing operation.
/// </summary>
public class TransitSignResult
{
    /// <summary>
    /// Vault-formatted signature (vault:v{version}:{algorithm}:...)
    /// </summary>
    public required string Signature { get; set; }

    /// <summary>
    /// Key version used for signing.
    /// </summary>
    public required int KeyVersion { get; set; }
}

/// <summary>
/// Result of a signature verification.
/// </summary>
public class TransitVerifyResult
{
    /// <summary>
    /// Whether the signature is valid.
    /// </summary>
    public required bool Valid { get; set; }
}

/// <summary>
/// Result of an HMAC operation.
/// </summary>
public class TransitHmacResult
{
    /// <summary>
    /// Vault-formatted HMAC (vault:v{version}:{algorithm}:...)
    /// </summary>
    public required string Hmac { get; set; }

    /// <summary>
    /// Key version used.
    /// </summary>
    public required int KeyVersion { get; set; }
}

/// <summary>
/// Result of a rewrap operation.
/// </summary>
public class TransitRewrapResult
{
    /// <summary>
    /// Ciphertext encrypted with latest key version.
    /// </summary>
    public required string Ciphertext { get; set; }

    /// <summary>
    /// New key version.
    /// </summary>
    public required int KeyVersion { get; set; }
}

/// <summary>
/// Information about a transit key.
/// </summary>
public class TransitKeyInfo
{
    /// <summary>
    /// Name of the key.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Type of key (aes256-gcm96, rsa-2048, etc.).
    /// </summary>
    public required string Type { get; set; }

    /// <summary>
    /// Whether key derivation is enabled.
    /// </summary>
    public required bool Derivation { get; set; }

    /// <summary>
    /// Whether the key can be exported.
    /// </summary>
    public required bool Exportable { get; set; }

    /// <summary>
    /// Whether deletion is allowed.
    /// </summary>
    public required bool DeletionAllowed { get; set; }

    /// <summary>
    /// Latest key version.
    /// </summary>
    public required int LatestVersion { get; set; }

    /// <summary>
    /// Minimum decryption version.
    /// </summary>
    public required int MinDecryptionVersion { get; set; }

    /// <summary>
    /// Minimum encryption version.
    /// </summary>
    public required int MinEncryptionVersion { get; set; }

    /// <summary>
    /// All key versions and their creation times.
    /// </summary>
    public Dictionary<int, DateTime> Keys { get; set; } = new();

    /// <summary>
    /// When the key was created.
    /// </summary>
    public required DateTime CreatedAt { get; set; }
}
