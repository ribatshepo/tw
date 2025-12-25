using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using USP.Core.Services.Cryptography;

namespace USP.Infrastructure.Services.Cryptography;

/// <summary>
/// Service for encrypting and decrypting data using AES-256-GCM
/// Provides authenticated encryption with associated data (AEAD)
/// </summary>
public class EncryptionService : IEncryptionService
{
    private readonly ISealManager _sealManager;
    private readonly ILogger<EncryptionService> _logger;
    private const int NonceSize = 12; // 96 bits recommended for GCM
    private const int TagSize = 16;   // 128 bits authentication tag

    public EncryptionService(ISealManager sealManager, ILogger<EncryptionService> logger)
    {
        _sealManager = sealManager;
        _logger = logger;
    }

    private byte[] GetMasterKey()
    {
        if (_sealManager.IsSealed())
        {
            throw new InvalidOperationException("System is sealed - unseal first to perform cryptographic operations");
        }

        var masterKey = _sealManager.GetMasterKey();
        if (masterKey == null || masterKey.Length != 32)
        {
            throw new InvalidOperationException("Invalid master key - must be 256 bits (32 bytes)");
        }

        return masterKey;
    }

    public string Encrypt(string plaintext)
    {
        var masterKey = GetMasterKey();
        return EncryptWithKey(plaintext, masterKey);
    }

    public string Decrypt(string encryptedData)
    {
        var masterKey = GetMasterKey();
        return DecryptWithKey(encryptedData, masterKey);
    }

    public string EncryptWithKey(string plaintext, byte[] key)
    {
        if (string.IsNullOrEmpty(plaintext))
        {
            throw new ArgumentNullException(nameof(plaintext));
        }

        if (key == null || key.Length != 32)
        {
            throw new ArgumentException("Key must be 256 bits (32 bytes)", nameof(key));
        }

        try
        {
            using var aesGcm = new AesGcm(key, TagSize);

            // Generate random nonce
            var nonce = new byte[NonceSize];
            RandomNumberGenerator.Fill(nonce);

            // Convert plaintext to bytes
            var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);

            // Allocate buffers
            var ciphertext = new byte[plaintextBytes.Length];
            var tag = new byte[TagSize];

            // Encrypt
            aesGcm.Encrypt(nonce, plaintextBytes, ciphertext, tag);

            // Combine nonce:tag:ciphertext and encode as Base64
            var result = new byte[NonceSize + TagSize + ciphertext.Length];
            Buffer.BlockCopy(nonce, 0, result, 0, NonceSize);
            Buffer.BlockCopy(tag, 0, result, NonceSize, TagSize);
            Buffer.BlockCopy(ciphertext, 0, result, NonceSize + TagSize, ciphertext.Length);

            return Convert.ToBase64String(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error encrypting data");
            throw new InvalidOperationException("Encryption failed", ex);
        }
    }

    public string DecryptWithKey(string encryptedData, byte[] key)
    {
        if (string.IsNullOrEmpty(encryptedData))
        {
            throw new ArgumentNullException(nameof(encryptedData));
        }

        if (key == null || key.Length != 32)
        {
            throw new ArgumentException("Key must be 256 bits (32 bytes)", nameof(key));
        }

        try
        {
            using var aesGcm = new AesGcm(key, TagSize);

            // Decode from Base64
            var encryptedBytes = Convert.FromBase64String(encryptedData);

            if (encryptedBytes.Length < NonceSize + TagSize)
            {
                throw new ArgumentException("Invalid encrypted data format");
            }

            // Extract nonce, tag, and ciphertext
            var nonce = new byte[NonceSize];
            var tag = new byte[TagSize];
            var ciphertext = new byte[encryptedBytes.Length - NonceSize - TagSize];

            Buffer.BlockCopy(encryptedBytes, 0, nonce, 0, NonceSize);
            Buffer.BlockCopy(encryptedBytes, NonceSize, tag, 0, TagSize);
            Buffer.BlockCopy(encryptedBytes, NonceSize + TagSize, ciphertext, 0, ciphertext.Length);

            // Decrypt
            var plaintext = new byte[ciphertext.Length];
            aesGcm.Decrypt(nonce, ciphertext, tag, plaintext);

            return Encoding.UTF8.GetString(plaintext);
        }
        catch (CryptographicException ex)
        {
            _logger.LogError(ex, "Decryption failed - authentication tag mismatch or data corrupted");
            throw new InvalidOperationException("Decryption failed - data may be corrupted or tampered with", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error decrypting data");
            throw new InvalidOperationException("Decryption failed", ex);
        }
    }

    public byte[] GenerateKey()
    {
        var key = new byte[32]; // 256 bits
        RandomNumberGenerator.Fill(key);
        _logger.LogInformation("Generated new 256-bit encryption key");
        return key;
    }
}
