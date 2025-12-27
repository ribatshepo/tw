namespace USP.Core.Domain.Entities.Secrets;

/// <summary>
/// Represents a transit encryption key used for encrypt/decrypt/sign/verify operations.
/// </summary>
public class TransitKey
{
    /// <summary>
    /// Unique identifier.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Name of the key (unique).
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Type of key (aes256-gcm96, rsa-2048, rsa-4096, ecdsa-p256, ecdsa-p384, ecdsa-p521, ed25519, chacha20-poly1305).
    /// </summary>
    public required string Type { get; set; }

    /// <summary>
    /// Whether key derivation is enabled.
    /// When enabled, a unique encryption key is derived for each context.
    /// </summary>
    public bool Derivation { get; set; }

    /// <summary>
    /// Whether the key can be exported.
    /// </summary>
    public bool Exportable { get; set; }

    /// <summary>
    /// Whether deletion is allowed.
    /// </summary>
    public bool DeletionAllowed { get; set; } = false;

    /// <summary>
    /// Latest version of the key.
    /// </summary>
    public int LatestVersion { get; set; } = 1;

    /// <summary>
    /// Minimum decryption version.
    /// Data encrypted with versions below this cannot be decrypted.
    /// </summary>
    public int MinDecryptionVersion { get; set; } = 1;

    /// <summary>
    /// Minimum encryption version.
    /// New encryptions must use this version or higher.
    /// </summary>
    public int MinEncryptionVersion { get; set; } = 1;

    /// <summary>
    /// Whether the key supports encryption/decryption.
    /// </summary>
    public bool SupportsEncryption { get; set; }

    /// <summary>
    /// Whether the key supports signing/verification.
    /// </summary>
    public bool SupportsSigning { get; set; }

    /// <summary>
    /// Whether the key supports key derivation.
    /// </summary>
    public bool SupportsDerivation { get; set; }

    /// <summary>
    /// Key versions (version number -> creation timestamp).
    /// Stored as JSON: {"1": "2025-01-01T00:00:00Z", "2": "2025-02-01T00:00:00Z"}
    /// </summary>
    public string KeyVersionsJson { get; set; } = "{}";

    /// <summary>
    /// Encrypted key material for all versions.
    /// Stored as JSON: {"1": "base64-encrypted-key", "2": "base64-encrypted-key"}
    /// Keys are encrypted with the master key from the seal service.
    /// </summary>
    public string EncryptedKeyMaterialJson { get; set; } = "{}";

    /// <summary>
    /// When the key was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the key was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Soft delete timestamp.
    /// </summary>
    public DateTime? DeletedAt { get; set; }
}
