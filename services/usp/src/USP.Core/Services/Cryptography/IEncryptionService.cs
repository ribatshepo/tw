namespace USP.Core.Services.Cryptography;

/// <summary>
/// Service for encrypting and decrypting data using AES-256-GCM
/// </summary>
public interface IEncryptionService
{
    /// <summary>
    /// Encrypt data using AES-256-GCM
    /// </summary>
    /// <param name="plaintext">Data to encrypt</param>
    /// <returns>Base64-encoded encrypted data (nonce:tag:ciphertext)</returns>
    string Encrypt(string plaintext);

    /// <summary>
    /// Decrypt data using AES-256-GCM
    /// </summary>
    /// <param name="encryptedData">Base64-encoded encrypted data (nonce:tag:ciphertext)</param>
    /// <returns>Decrypted plaintext</returns>
    string Decrypt(string encryptedData);

    /// <summary>
    /// Encrypt data with a specific key
    /// </summary>
    string EncryptWithKey(string plaintext, byte[] key);

    /// <summary>
    /// Decrypt data with a specific key
    /// </summary>
    string DecryptWithKey(string encryptedData, byte[] key);

    /// <summary>
    /// Generate a new encryption key
    /// </summary>
    byte[] GenerateKey();
}
