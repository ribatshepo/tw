using USP.Core.Models.DTOs.Transit;

namespace USP.Core.Services.Secrets;

/// <summary>
/// Transit Engine - Encryption as a Service
/// Provides named encryption keys with versioning support for application-layer encryption
/// Compatible with HashiCorp Vault Transit Engine API
/// </summary>
public interface ITransitEngine
{
    // ============================================
    // Key Management Operations
    // ============================================

    /// <summary>
    /// Create a new named encryption key
    /// </summary>
    /// <param name="keyName">Unique name for the key (e.g., "customer-data-key")</param>
    /// <param name="request">Key creation parameters</param>
    /// <param name="userId">User creating the key</param>
    /// <returns>Created key metadata</returns>
    Task<CreateKeyResponse> CreateKeyAsync(string keyName, CreateKeyRequest request, Guid userId);

    /// <summary>
    /// Read key metadata and configuration
    /// </summary>
    /// <param name="keyName">Name of the key</param>
    /// <param name="userId">User requesting key info</param>
    /// <returns>Key metadata including all versions</returns>
    Task<ReadKeyResponse> ReadKeyAsync(string keyName, Guid userId);

    /// <summary>
    /// List all transit keys accessible to the user
    /// </summary>
    /// <param name="userId">User requesting the list</param>
    /// <returns>List of key names</returns>
    Task<ListKeysResponse> ListKeysAsync(Guid userId);

    /// <summary>
    /// Delete a transit key (only if DeletionAllowed is true)
    /// </summary>
    /// <param name="keyName">Name of the key to delete</param>
    /// <param name="userId">User deleting the key</param>
    Task DeleteKeyAsync(string keyName, Guid userId);

    /// <summary>
    /// Update key configuration (min versions, deletion policy)
    /// </summary>
    /// <param name="keyName">Name of the key</param>
    /// <param name="request">Configuration updates</param>
    /// <param name="userId">User updating the key</param>
    /// <returns>Updated key configuration</returns>
    Task<UpdateKeyConfigResponse> UpdateKeyConfigAsync(string keyName, UpdateKeyConfigRequest request, Guid userId);

    /// <summary>
    /// Rotate a key (create a new version)
    /// </summary>
    /// <param name="keyName">Name of the key to rotate</param>
    /// <param name="userId">User rotating the key</param>
    /// <returns>New key version number</returns>
    Task<RotateKeyResponse> RotateKeyAsync(string keyName, Guid userId);

    // ============================================
    // Encryption Operations (Symmetric Keys)
    // ============================================

    /// <summary>
    /// Encrypt plaintext data using the named key
    /// </summary>
    /// <param name="keyName">Name of the encryption key</param>
    /// <param name="request">Encryption parameters (plaintext, context, version)</param>
    /// <param name="userId">User performing encryption</param>
    /// <returns>Ciphertext in vault format: vault:v{version}:{base64_ciphertext}</returns>
    Task<EncryptResponse> EncryptAsync(string keyName, EncryptRequest request, Guid userId);

    /// <summary>
    /// Decrypt ciphertext using the named key
    /// </summary>
    /// <param name="keyName">Name of the encryption key</param>
    /// <param name="request">Decryption parameters (ciphertext, context)</param>
    /// <param name="userId">User performing decryption</param>
    /// <returns>Base64-encoded plaintext</returns>
    Task<DecryptResponse> DecryptAsync(string keyName, DecryptRequest request, Guid userId);

    /// <summary>
    /// Rewrap ciphertext (re-encrypt with latest key version)
    /// Useful for key rotation without decrypting to plaintext in application
    /// </summary>
    /// <param name="keyName">Name of the encryption key</param>
    /// <param name="request">Rewrap parameters (ciphertext, context)</param>
    /// <param name="userId">User performing rewrap</param>
    /// <returns>Re-encrypted ciphertext with latest version</returns>
    Task<RewrapResponse> RewrapAsync(string keyName, RewrapRequest request, Guid userId);

    // ============================================
    // Batch Operations
    // ============================================

    /// <summary>
    /// Encrypt multiple plaintexts in a single operation
    /// </summary>
    /// <param name="keyName">Name of the encryption key</param>
    /// <param name="request">Batch of plaintexts to encrypt</param>
    /// <param name="userId">User performing batch encryption</param>
    /// <returns>Batch of ciphertexts (max 1000 items)</returns>
    Task<BatchEncryptResponse> BatchEncryptAsync(string keyName, BatchEncryptRequest request, Guid userId);

    /// <summary>
    /// Decrypt multiple ciphertexts in a single operation
    /// </summary>
    /// <param name="keyName">Name of the encryption key</param>
    /// <param name="request">Batch of ciphertexts to decrypt</param>
    /// <param name="userId">User performing batch decryption</param>
    /// <returns>Batch of plaintexts (max 1000 items)</returns>
    Task<BatchDecryptResponse> BatchDecryptAsync(string keyName, BatchDecryptRequest request, Guid userId);

    // ============================================
    // Data Key Generation (Envelope Encryption)
    // ============================================

    /// <summary>
    /// Generate a high-entropy data encryption key (DEK)
    /// Returns both plaintext key (for immediate use) and encrypted key (for storage)
    /// Used for envelope encryption pattern
    /// </summary>
    /// <param name="keyName">Name of the key-encryption-key (KEK)</param>
    /// <param name="request">Data key generation parameters</param>
    /// <param name="userId">User generating the data key</param>
    /// <returns>Plaintext and encrypted data key</returns>
    Task<GenerateDataKeyResponse> GenerateDataKeyAsync(string keyName, GenerateDataKeyRequest request, Guid userId);

    // ============================================
    // Signing Operations (Asymmetric Keys)
    // ============================================

    /// <summary>
    /// Sign data using an asymmetric key
    /// Supported for: rsa-2048, rsa-4096, ed25519, ecdsa-p256
    /// </summary>
    /// <param name="keyName">Name of the signing key</param>
    /// <param name="request">Signing parameters (data, hash algorithm)</param>
    /// <param name="userId">User performing signing</param>
    /// <returns>Digital signature</returns>
    Task<SignResponse> SignAsync(string keyName, SignRequest request, Guid userId);

    /// <summary>
    /// Verify a signature using an asymmetric key
    /// Supported for: rsa-2048, rsa-4096, ed25519, ecdsa-p256
    /// </summary>
    /// <param name="keyName">Name of the signing key</param>
    /// <param name="request">Verification parameters (data, signature, hash algorithm)</param>
    /// <param name="userId">User performing verification</param>
    /// <returns>True if signature is valid</returns>
    Task<VerifyResponse> VerifyAsync(string keyName, VerifyRequest request, Guid userId);
}
