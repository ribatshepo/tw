namespace USP.Core.Services.Cryptography;

/// <summary>
/// Hardware Security Module (HSM) connector for cryptographic operations
/// </summary>
public interface IHsmConnector
{
    /// <summary>
    /// Check if HSM is available and responding
    /// </summary>
    Task<bool> IsAvailableAsync();

    /// <summary>
    /// Generate a key in HSM
    /// </summary>
    Task<string> GenerateKeyAsync(string keyId, HsmKeyType keyType, int keySize);

    /// <summary>
    /// Sign data using HSM-stored key
    /// </summary>
    Task<byte[]> SignAsync(string keyId, byte[] data, HsmSignatureAlgorithm algorithm);

    /// <summary>
    /// Verify signature using HSM-stored key
    /// </summary>
    Task<bool> VerifyAsync(string keyId, byte[] data, byte[] signature, HsmSignatureAlgorithm algorithm);

    /// <summary>
    /// Encrypt data using HSM-stored key
    /// </summary>
    Task<byte[]> EncryptAsync(string keyId, byte[] plaintext, HsmEncryptionAlgorithm algorithm);

    /// <summary>
    /// Decrypt data using HSM-stored key
    /// </summary>
    Task<byte[]> DecryptAsync(string keyId, byte[] ciphertext, HsmEncryptionAlgorithm algorithm);

    /// <summary>
    /// Delete key from HSM
    /// </summary>
    Task DeleteKeyAsync(string keyId);

    /// <summary>
    /// List all keys in HSM
    /// </summary>
    Task<List<HsmKeyInfo>> ListKeysAsync();

    /// <summary>
    /// Get key information
    /// </summary>
    Task<HsmKeyInfo?> GetKeyInfoAsync(string keyId);

    /// <summary>
    /// Rotate key (generate new version)
    /// </summary>
    Task<string> RotateKeyAsync(string keyId);

    /// <summary>
    /// Export public key
    /// </summary>
    Task<byte[]> ExportPublicKeyAsync(string keyId);

    /// <summary>
    /// Import key into HSM
    /// </summary>
    Task<string> ImportKeyAsync(string keyId, byte[] keyMaterial, HsmKeyType keyType);

    /// <summary>
    /// Generate random bytes using HSM RNG
    /// </summary>
    Task<byte[]> GenerateRandomAsync(int byteCount);
}

/// <summary>
/// HSM key type
/// </summary>
public enum HsmKeyType
{
    Rsa2048,
    Rsa4096,
    EcdsaP256,
    EcdsaP384,
    EcdsaP521,
    Aes128,
    Aes256,
    Ed25519
}

/// <summary>
/// HSM signature algorithm
/// </summary>
public enum HsmSignatureAlgorithm
{
    RsaSha256,
    RsaSha384,
    RsaSha512,
    EcdsaSha256,
    EcdsaSha384,
    EcdsaSha512,
    Ed25519
}

/// <summary>
/// HSM encryption algorithm
/// </summary>
public enum HsmEncryptionAlgorithm
{
    RsaOaepSha256,
    RsaOaepSha384,
    RsaOaepSha512,
    AesGcm,
    AesCbc
}

/// <summary>
/// HSM key information
/// </summary>
public class HsmKeyInfo
{
    public string KeyId { get; set; } = string.Empty;
    public HsmKeyType KeyType { get; set; }
    public int KeySize { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool IsExportable { get; set; }
    public string[] AllowedOperations { get; set; } = Array.Empty<string>();
    public int Version { get; set; }
}
