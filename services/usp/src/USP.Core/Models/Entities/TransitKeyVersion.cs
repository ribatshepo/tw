namespace USP.Core.Models.Entities;

/// <summary>
/// Individual version of a transit key
/// Each version has its own cryptographic key material encrypted with master key
/// </summary>
public class TransitKeyVersion
{
    public Guid Id { get; set; }
    public Guid TransitKeyId { get; set; }
    public int Version { get; set; } // Version number (1, 2, 3...)

    // Encrypted key material (encrypted with SealManager master key)
    public string EncryptedKeyMaterial { get; set; } = string.Empty; // Base64 encoded

    // For asymmetric keys, store public key separately (not encrypted)
    public string? PublicKey { get; set; } // PEM format for RSA/ECDSA/Ed25519

    // Key metadata
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid CreatedBy { get; set; }
    public DateTime? ArchivedAt { get; set; } // When key version was archived
    public DateTime? DestroyedAt { get; set; } // When key version was destroyed

    // Navigation properties
    public virtual TransitKey? TransitKey { get; set; }
    public virtual ApplicationUser? Creator { get; set; }
}
