namespace USP.Core.Domain.Enums;

/// <summary>
/// Represents the encryption algorithm used in Transit engine
/// </summary>
public enum EncryptionAlgorithm
{
    /// <summary>
    /// AES-256-GCM (Advanced Encryption Standard with Galois/Counter Mode)
    /// </summary>
    AES256GCM = 0,

    /// <summary>
    /// ChaCha20-Poly1305 (stream cipher with AEAD)
    /// </summary>
    ChaCha20Poly1305 = 1,

    /// <summary>
    /// RSA-2048 (asymmetric encryption)
    /// </summary>
    RSA2048 = 2,

    /// <summary>
    /// RSA-4096 (asymmetric encryption)
    /// </summary>
    RSA4096 = 3,

    /// <summary>
    /// ED25519 (Edwards-curve Digital Signature Algorithm)
    /// </summary>
    ED25519 = 4
}
