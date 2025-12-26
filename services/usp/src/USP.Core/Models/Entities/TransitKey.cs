namespace USP.Core.Models.Entities;

/// <summary>
/// Transit encryption key with versioning support
/// Supports multiple key types: symmetric (AES, ChaCha20) and asymmetric (RSA, Ed25519, ECDSA)
/// </summary>
public class TransitKey
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty; // Unique key name (e.g., "customer-data-key")
    public string Type { get; set; } = string.Empty; // aes256-gcm96, chacha20-poly1305, rsa-2048, rsa-4096, ed25519, ecdsa-p256
    public int LatestVersion { get; set; } = 1; // Current key version
    public int MinDecryptionVersion { get; set; } = 1; // Minimum version allowed for decryption
    public int MinEncryptionVersion { get; set; } = 1; // Minimum version for encryption (usually = LatestVersion)
    public bool DeletionAllowed { get; set; } = false; // Whether key can be deleted
    public bool Exportable { get; set; } = false; // Whether key can be exported
    public bool AllowPlaintextBackup { get; set; } = false; // Allow backup of plaintext key
    public bool ConvergentEncryption { get; set; } = false; // Same plaintext + context = same ciphertext
    public int? ConvergentVersion { get; set; } // Version when convergent encryption was enabled
    public bool Derived { get; set; } = false; // Support key derivation from context

    // Key usage tracking
    public long EncryptionCount { get; set; } = 0;
    public long DecryptionCount { get; set; } = 0;
    public long SigningCount { get; set; } = 0;
    public long VerificationCount { get; set; } = 0;

    // Metadata
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual ApplicationUser? Creator { get; set; }
    public virtual ICollection<TransitKeyVersion> Versions { get; set; } = new List<TransitKeyVersion>();
}
