using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using USP.Core.Models.DTOs.Transit;
using USP.Core.Models.Entities;
using USP.Core.Services.Cryptography;
using USP.Core.Services.Secrets;
using USP.Infrastructure.Data;

namespace USP.Infrastructure.Services.Secrets;

/// <summary>
/// Transit Engine - Encryption as a Service
/// Provides Vault-compatible transit encryption with named keys and versioning
/// </summary>
public class TransitEngine : ITransitEngine
{
    private readonly ApplicationDbContext _context;
    private readonly IEncryptionService _encryptionService;
    private readonly ILogger<TransitEngine> _logger;

    // Supported key types
    private static readonly HashSet<string> SupportedKeyTypes = new()
    {
        "aes256-gcm96",      // AES-256-GCM with 96-bit nonce
        "chacha20-poly1305", // ChaCha20-Poly1305 AEAD
        "rsa-2048",          // RSA 2048-bit
        "rsa-4096",          // RSA 4096-bit
        "ecdsa-p256"         // ECDSA with P-256 curve
    };

    // Symmetric key types
    private static readonly HashSet<string> SymmetricKeyTypes = new()
    {
        "aes256-gcm96",
        "chacha20-poly1305"
    };

    // Asymmetric key types
    private static readonly HashSet<string> AsymmetricKeyTypes = new()
    {
        "rsa-2048",
        "rsa-4096",
        "ecdsa-p256"
    };

    public TransitEngine(
        ApplicationDbContext context,
        IEncryptionService encryptionService,
        ILogger<TransitEngine> logger)
    {
        _context = context;
        _encryptionService = encryptionService;
        _logger = logger;
    }

    // ============================================
    // Key Management Operations
    // ============================================

    public async Task<CreateKeyResponse> CreateKeyAsync(string keyName, CreateKeyRequest request, Guid userId)
    {
        if (string.IsNullOrWhiteSpace(keyName))
            throw new ArgumentException("Key name cannot be empty", nameof(keyName));

        if (!SupportedKeyTypes.Contains(request.Type))
            throw new ArgumentException($"Unsupported key type: {request.Type}. Supported types: {string.Join(", ", SupportedKeyTypes)}");

        // Check if key already exists
        if (await _context.TransitKeys.AnyAsync(tk => tk.Name == keyName))
            throw new InvalidOperationException($"Key '{keyName}' already exists");

        // Generate cryptographic key material based on type
        var (encryptedKeyMaterial, publicKey) = await GenerateKeyMaterialAsync(request.Type);

        // Create transit key
        var transitKey = new TransitKey
        {
            Id = Guid.NewGuid(),
            Name = keyName,
            Type = request.Type,
            LatestVersion = 1,
            MinDecryptionVersion = 1,
            MinEncryptionVersion = 1,
            DeletionAllowed = request.DeletionAllowed,
            Exportable = request.Exportable,
            AllowPlaintextBackup = request.AllowPlaintextBackup,
            ConvergentEncryption = request.ConvergentEncryption,
            Derived = request.Derived,
            CreatedBy = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Create first key version
        var keyVersion = new TransitKeyVersion
        {
            Id = Guid.NewGuid(),
            TransitKeyId = transitKey.Id,
            Version = 1,
            EncryptedKeyMaterial = encryptedKeyMaterial,
            PublicKey = publicKey,
            CreatedBy = userId,
            CreatedAt = DateTime.UtcNow
        };

        _context.TransitKeys.Add(transitKey);
        _context.TransitKeyVersions.Add(keyVersion);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created transit key '{KeyName}' of type '{KeyType}' by user {UserId}", keyName, request.Type, userId);

        return new CreateKeyResponse
        {
            Name = transitKey.Name,
            Type = transitKey.Type,
            LatestVersion = transitKey.LatestVersion
        };
    }

    public async Task<ReadKeyResponse> ReadKeyAsync(string keyName, Guid userId)
    {
        var key = await _context.TransitKeys
            .Include(tk => tk.Versions)
            .FirstOrDefaultAsync(tk => tk.Name == keyName)
            ?? throw new KeyNotFoundException($"Key '{keyName}' not found");

        var versions = key.Versions.ToDictionary(
            v => v.Version,
            v => new KeyVersionInfo
            {
                CreatedAt = v.CreatedAt,
                ArchivedAt = v.ArchivedAt,
                DestroyedAt = v.DestroyedAt,
                PublicKey = v.PublicKey
            });

        return new ReadKeyResponse
        {
            Name = key.Name,
            Type = key.Type,
            LatestVersion = key.LatestVersion,
            MinDecryptionVersion = key.MinDecryptionVersion,
            MinEncryptionVersion = key.MinEncryptionVersion,
            DeletionAllowed = key.DeletionAllowed,
            Exportable = key.Exportable,
            AllowPlaintextBackup = key.AllowPlaintextBackup,
            ConvergentEncryption = key.ConvergentEncryption,
            Derived = key.Derived,
            EncryptionCount = key.EncryptionCount,
            DecryptionCount = key.DecryptionCount,
            SigningCount = key.SigningCount,
            VerificationCount = key.VerificationCount,
            CreatedAt = key.CreatedAt,
            UpdatedAt = key.UpdatedAt,
            Versions = versions
        };
    }

    public async Task<ListKeysResponse> ListKeysAsync(Guid userId)
    {
        var keys = await _context.TransitKeys
            .OrderBy(tk => tk.Name)
            .Select(tk => tk.Name)
            .ToListAsync();

        return new ListKeysResponse { Keys = keys };
    }

    public async Task DeleteKeyAsync(string keyName, Guid userId)
    {
        var key = await _context.TransitKeys
            .FirstOrDefaultAsync(tk => tk.Name == keyName)
            ?? throw new KeyNotFoundException($"Key '{keyName}' not found");

        if (!key.DeletionAllowed)
            throw new InvalidOperationException($"Key '{keyName}' cannot be deleted. Set DeletionAllowed to true first.");

        _context.TransitKeys.Remove(key);
        await _context.SaveChangesAsync();

        _logger.LogWarning("Deleted transit key '{KeyName}' by user {UserId}", keyName, userId);
    }

    public async Task<UpdateKeyConfigResponse> UpdateKeyConfigAsync(string keyName, UpdateKeyConfigRequest request, Guid userId)
    {
        var key = await _context.TransitKeys
            .FirstOrDefaultAsync(tk => tk.Name == keyName)
            ?? throw new KeyNotFoundException($"Key '{keyName}' not found");

        if (request.MinDecryptionVersion.HasValue)
        {
            if (request.MinDecryptionVersion.Value < 1 || request.MinDecryptionVersion.Value > key.LatestVersion)
                throw new ArgumentException($"MinDecryptionVersion must be between 1 and {key.LatestVersion}");
            key.MinDecryptionVersion = request.MinDecryptionVersion.Value;
        }

        if (request.MinEncryptionVersion.HasValue)
        {
            if (request.MinEncryptionVersion.Value < 1 || request.MinEncryptionVersion.Value > key.LatestVersion)
                throw new ArgumentException($"MinEncryptionVersion must be between 1 and {key.LatestVersion}");
            key.MinEncryptionVersion = request.MinEncryptionVersion.Value;
        }

        if (request.DeletionAllowed.HasValue)
            key.DeletionAllowed = request.DeletionAllowed.Value;

        key.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Updated configuration for transit key '{KeyName}' by user {UserId}", keyName, userId);

        return new UpdateKeyConfigResponse
        {
            Name = key.Name,
            MinDecryptionVersion = key.MinDecryptionVersion,
            MinEncryptionVersion = key.MinEncryptionVersion,
            DeletionAllowed = key.DeletionAllowed
        };
    }

    public async Task<RotateKeyResponse> RotateKeyAsync(string keyName, Guid userId)
    {
        var key = await _context.TransitKeys
            .FirstOrDefaultAsync(tk => tk.Name == keyName)
            ?? throw new KeyNotFoundException($"Key '{keyName}' not found");

        // Generate new key material
        var (encryptedKeyMaterial, publicKey) = await GenerateKeyMaterialAsync(key.Type);

        // Create new version
        var newVersion = key.LatestVersion + 1;
        var keyVersion = new TransitKeyVersion
        {
            Id = Guid.NewGuid(),
            TransitKeyId = key.Id,
            Version = newVersion,
            EncryptedKeyMaterial = encryptedKeyMaterial,
            PublicKey = publicKey,
            CreatedBy = userId,
            CreatedAt = DateTime.UtcNow
        };

        key.LatestVersion = newVersion;
        key.MinEncryptionVersion = newVersion; // Force new encryptions to use latest version
        key.UpdatedAt = DateTime.UtcNow;

        _context.TransitKeyVersions.Add(keyVersion);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Rotated transit key '{KeyName}' to version {Version} by user {UserId}", keyName, newVersion, userId);

        return new RotateKeyResponse
        {
            Name = key.Name,
            LatestVersion = key.LatestVersion
        };
    }

    // ============================================
    // Encryption Operations
    // ============================================

    public async Task<EncryptResponse> EncryptAsync(string keyName, EncryptRequest request, Guid userId)
    {
        var key = await _context.TransitKeys
            .Include(tk => tk.Versions)
            .FirstOrDefaultAsync(tk => tk.Name == keyName)
            ?? throw new KeyNotFoundException($"Key '{keyName}' not found");

        if (!SymmetricKeyTypes.Contains(key.Type))
            throw new InvalidOperationException($"Key '{keyName}' of type '{key.Type}' does not support encryption. Use signing keys for signing operations.");

        // Determine which version to use
        var version = request.KeyVersion ?? key.MinEncryptionVersion;
        if (version < key.MinEncryptionVersion || version > key.LatestVersion)
            throw new ArgumentException($"Key version {version} is not allowed for encryption. Valid range: {key.MinEncryptionVersion}-{key.LatestVersion}");

        var keyVersion = key.Versions.FirstOrDefault(v => v.Version == version)
            ?? throw new KeyNotFoundException($"Key version {version} not found");

        // Decrypt the key material from storage
        var keyMaterial = await DecryptKeyMaterialAsync(keyVersion.EncryptedKeyMaterial);

        // Perform encryption based on key type
        string ciphertext;
        if (key.Type == "aes256-gcm96")
        {
            ciphertext = await EncryptWithAesGcmAsync(request.Plaintext, keyMaterial, request.Context);
        }
        else if (key.Type == "chacha20-poly1305")
        {
            ciphertext = await EncryptWithChaCha20Poly1305Async(request.Plaintext, keyMaterial, request.Context);
        }
        else
        {
            throw new NotSupportedException($"Encryption not supported for key type '{key.Type}'");
        }

        // Update usage statistics
        key.EncryptionCount++;
        await _context.SaveChangesAsync();

        // Return in Vault format: vault:v{version}:{ciphertext}
        var vaultCiphertext = $"vault:v{version}:{ciphertext}";

        return new EncryptResponse
        {
            Ciphertext = vaultCiphertext,
            KeyVersion = version
        };
    }

    public async Task<DecryptResponse> DecryptAsync(string keyName, DecryptRequest request, Guid userId)
    {
        var key = await _context.TransitKeys
            .Include(tk => tk.Versions)
            .FirstOrDefaultAsync(tk => tk.Name == keyName)
            ?? throw new KeyNotFoundException($"Key '{keyName}' not found");

        if (!SymmetricKeyTypes.Contains(key.Type))
            throw new InvalidOperationException($"Key '{keyName}' of type '{key.Type}' does not support decryption");

        // Parse vault format: vault:v{version}:{ciphertext}
        var (version, ciphertext) = ParseVaultCiphertext(request.Ciphertext);

        if (version < key.MinDecryptionVersion)
            throw new InvalidOperationException($"Key version {version} is below minimum allowed decryption version {key.MinDecryptionVersion}");

        var keyVersion = key.Versions.FirstOrDefault(v => v.Version == version)
            ?? throw new KeyNotFoundException($"Key version {version} not found");

        if (keyVersion.DestroyedAt.HasValue)
            throw new InvalidOperationException($"Key version {version} has been destroyed and cannot be used for decryption");

        // Decrypt the key material from storage
        var keyMaterial = await DecryptKeyMaterialAsync(keyVersion.EncryptedKeyMaterial);

        // Perform decryption based on key type
        string plaintext;
        if (key.Type == "aes256-gcm96")
        {
            plaintext = await DecryptWithAesGcmAsync(ciphertext, keyMaterial, request.Context);
        }
        else if (key.Type == "chacha20-poly1305")
        {
            plaintext = await DecryptWithChaCha20Poly1305Async(ciphertext, keyMaterial, request.Context);
        }
        else
        {
            throw new NotSupportedException($"Decryption not supported for key type '{key.Type}'");
        }

        // Update usage statistics
        key.DecryptionCount++;
        await _context.SaveChangesAsync();

        return new DecryptResponse
        {
            Plaintext = plaintext,
            KeyVersion = version
        };
    }

    public async Task<RewrapResponse> RewrapAsync(string keyName, RewrapRequest request, Guid userId)
    {
        // Decrypt with old version
        var decryptResponse = await DecryptAsync(keyName, new DecryptRequest
        {
            Ciphertext = request.Ciphertext,
            Context = request.Context
        }, userId);

        // Encrypt with latest version
        var encryptResponse = await EncryptAsync(keyName, new EncryptRequest
        {
            Plaintext = decryptResponse.Plaintext,
            Context = request.Context
        }, userId);

        return new RewrapResponse
        {
            Ciphertext = encryptResponse.Ciphertext,
            KeyVersion = encryptResponse.KeyVersion
        };
    }

    // ============================================
    // Batch Operations
    // ============================================

    public async Task<BatchEncryptResponse> BatchEncryptAsync(string keyName, BatchEncryptRequest request, Guid userId)
    {
        if (request.BatchInput.Count > 1000)
            throw new ArgumentException("Batch size cannot exceed 1000 items");

        var results = new List<BatchEncryptResponseItem>();

        foreach (var item in request.BatchInput)
        {
            try
            {
                var response = await EncryptAsync(keyName, new EncryptRequest
                {
                    Plaintext = item.Plaintext,
                    Context = item.Context
                }, userId);

                results.Add(new BatchEncryptResponseItem
                {
                    Ciphertext = response.Ciphertext,
                    KeyVersion = response.KeyVersion
                });
            }
            catch (Exception ex)
            {
                results.Add(new BatchEncryptResponseItem
                {
                    Error = ex.Message
                });
            }
        }

        return new BatchEncryptResponse { BatchResults = results };
    }

    public async Task<BatchDecryptResponse> BatchDecryptAsync(string keyName, BatchDecryptRequest request, Guid userId)
    {
        if (request.BatchInput.Count > 1000)
            throw new ArgumentException("Batch size cannot exceed 1000 items");

        var results = new List<BatchDecryptResponseItem>();

        foreach (var item in request.BatchInput)
        {
            try
            {
                var response = await DecryptAsync(keyName, new DecryptRequest
                {
                    Ciphertext = item.Ciphertext,
                    Context = item.Context
                }, userId);

                results.Add(new BatchDecryptResponseItem
                {
                    Plaintext = response.Plaintext,
                    KeyVersion = response.KeyVersion
                });
            }
            catch (Exception ex)
            {
                results.Add(new BatchDecryptResponseItem
                {
                    Error = ex.Message
                });
            }
        }

        return new BatchDecryptResponse { BatchResults = results };
    }

    // ============================================
    // Data Key Generation
    // ============================================

    public async Task<GenerateDataKeyResponse> GenerateDataKeyAsync(string keyName, GenerateDataKeyRequest request, Guid userId)
    {
        if (request.Bits != 256 && request.Bits != 512)
            throw new ArgumentException("Data key size must be 256 or 512 bits");

        // Generate random data encryption key
        var dataKey = new byte[request.Bits / 8];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(dataKey);
        }

        var plaintextDek = Convert.ToBase64String(dataKey);

        // Encrypt the DEK using the transit key
        var encryptResponse = await EncryptAsync(keyName, new EncryptRequest
        {
            Plaintext = plaintextDek,
            Context = request.Context
        }, userId);

        return new GenerateDataKeyResponse
        {
            Plaintext = plaintextDek,
            Ciphertext = encryptResponse.Ciphertext,
            KeyVersion = encryptResponse.KeyVersion
        };
    }

    // ============================================
    // Signing Operations (Asymmetric Keys)
    // ============================================

    public async Task<SignResponse> SignAsync(string keyName, SignRequest request, Guid userId)
    {
        var key = await _context.TransitKeys
            .Include(tk => tk.Versions)
            .FirstOrDefaultAsync(tk => tk.Name == keyName)
            ?? throw new KeyNotFoundException($"Key '{keyName}' not found");

        if (!AsymmetricKeyTypes.Contains(key.Type))
            throw new InvalidOperationException($"Key '{keyName}' of type '{key.Type}' does not support signing");

        var keyVersion = key.Versions.FirstOrDefault(v => v.Version == key.LatestVersion)
            ?? throw new KeyNotFoundException($"Key version {key.LatestVersion} not found");

        // Decrypt the key material
        var keyMaterial = await DecryptKeyMaterialAsync(keyVersion.EncryptedKeyMaterial);

        // Perform signing based on key type
        var signature = await SignDataAsync(request.Input, keyMaterial, key.Type, request.HashAlgorithm);

        // Update usage statistics
        key.SigningCount++;
        await _context.SaveChangesAsync();

        return new SignResponse
        {
            Signature = signature,
            KeyVersion = key.LatestVersion
        };
    }

    public async Task<VerifyResponse> VerifyAsync(string keyName, VerifyRequest request, Guid userId)
    {
        var key = await _context.TransitKeys
            .Include(tk => tk.Versions)
            .FirstOrDefaultAsync(tk => tk.Name == keyName)
            ?? throw new KeyNotFoundException($"Key '{keyName}' not found");

        if (!AsymmetricKeyTypes.Contains(key.Type))
            throw new InvalidOperationException($"Key '{keyName}' of type '{key.Type}' does not support signature verification");

        // Signature verification uses the public key from the latest version
        // Future enhancement: Parse signature format to extract embedded version number
        var keyVersion = key.Versions.FirstOrDefault(v => v.Version == key.LatestVersion)
            ?? throw new KeyNotFoundException($"Key version {key.LatestVersion} not found");

        if (string.IsNullOrEmpty(keyVersion.PublicKey))
            throw new InvalidOperationException($"Public key not available for verification");

        // Verify signature
        var isValid = await VerifySignatureAsync(request.Input, request.Signature, keyVersion.PublicKey, key.Type, request.HashAlgorithm);

        // Update usage statistics
        key.VerificationCount++;
        await _context.SaveChangesAsync();

        return new VerifyResponse
        {
            Valid = isValid,
            KeyVersion = key.LatestVersion
        };
    }

    // ============================================
    // Helper Methods
    // ============================================

    private async Task<(string encryptedKeyMaterial, string? publicKey)> GenerateKeyMaterialAsync(string keyType)
    {
        byte[] keyMaterial;
        string? publicKey = null;

        if (keyType == "aes256-gcm96")
        {
            // Generate 256-bit AES key
            keyMaterial = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(keyMaterial);
            }
        }
        else if (keyType == "chacha20-poly1305")
        {
            // Generate 256-bit ChaCha20 key
            keyMaterial = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(keyMaterial);
            }
        }
        else if (keyType == "rsa-2048" || keyType == "rsa-4096")
        {
            // Generate RSA key pair
            var keySize = keyType == "rsa-2048" ? 2048 : 4096;
            using var rsa = RSA.Create(keySize);
            keyMaterial = rsa.ExportRSAPrivateKey();
            publicKey = Convert.ToBase64String(rsa.ExportRSAPublicKey());
        }
        else if (keyType == "ecdsa-p256")
        {
            // Generate ECDSA P-256 key pair
            using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            keyMaterial = ecdsa.ExportECPrivateKey();
            publicKey = Convert.ToBase64String(ecdsa.ExportSubjectPublicKeyInfo());
        }
        else
        {
            throw new NotSupportedException($"Key type '{keyType}' is not supported");
        }

        // Encrypt the key material with the master key
        var keyMaterialBase64 = Convert.ToBase64String(keyMaterial);
        var encryptedKeyMaterial = _encryptionService.Encrypt(keyMaterialBase64);

        return (encryptedKeyMaterial, publicKey);
    }

    private Task<byte[]> DecryptKeyMaterialAsync(string encryptedKeyMaterial)
    {
        var decryptedBase64 = _encryptionService.Decrypt(encryptedKeyMaterial);
        return Task.FromResult(Convert.FromBase64String(decryptedBase64));
    }

    private async Task<string> EncryptWithAesGcmAsync(string plaintextBase64, byte[] key, string? contextBase64)
    {
        var plaintext = Convert.FromBase64String(plaintextBase64);
        var additionalData = contextBase64 != null ? Convert.FromBase64String(contextBase64) : null;

        using var aesGcm = new AesGcm(key, 16); // 128-bit tag
        var nonce = new byte[12]; // 96-bit nonce
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[16];

        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(nonce);
        }

        aesGcm.Encrypt(nonce, plaintext, ciphertext, tag, additionalData);

        // Combine: nonce + tag + ciphertext
        var combined = new byte[nonce.Length + tag.Length + ciphertext.Length];
        Buffer.BlockCopy(nonce, 0, combined, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, combined, nonce.Length, tag.Length);
        Buffer.BlockCopy(ciphertext, 0, combined, nonce.Length + tag.Length, ciphertext.Length);

        return Convert.ToBase64String(combined);
    }

    private async Task<string> DecryptWithAesGcmAsync(string ciphertextBase64, byte[] key, string? contextBase64)
    {
        var combined = Convert.FromBase64String(ciphertextBase64);
        var additionalData = contextBase64 != null ? Convert.FromBase64String(contextBase64) : null;

        var nonce = new byte[12];
        var tag = new byte[16];
        var ciphertext = new byte[combined.Length - 28]; // 12 + 16

        Buffer.BlockCopy(combined, 0, nonce, 0, 12);
        Buffer.BlockCopy(combined, 12, tag, 0, 16);
        Buffer.BlockCopy(combined, 28, ciphertext, 0, ciphertext.Length);

        using var aesGcm = new AesGcm(key, 16);
        var plaintext = new byte[ciphertext.Length];

        aesGcm.Decrypt(nonce, ciphertext, tag, plaintext, additionalData);

        return Convert.ToBase64String(plaintext);
    }

    private async Task<string> EncryptWithChaCha20Poly1305Async(string plaintextBase64, byte[] key, string? contextBase64)
    {
        var plaintext = Convert.FromBase64String(plaintextBase64);
        var additionalData = contextBase64 != null ? Convert.FromBase64String(contextBase64) : null;

        using var chacha = new ChaCha20Poly1305(key);
        var nonce = new byte[12];
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[16];

        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(nonce);
        }

        chacha.Encrypt(nonce, plaintext, ciphertext, tag, additionalData);

        // Combine: nonce + tag + ciphertext
        var combined = new byte[nonce.Length + tag.Length + ciphertext.Length];
        Buffer.BlockCopy(nonce, 0, combined, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, combined, nonce.Length, tag.Length);
        Buffer.BlockCopy(ciphertext, 0, combined, nonce.Length + tag.Length, ciphertext.Length);

        return Convert.ToBase64String(combined);
    }

    private async Task<string> DecryptWithChaCha20Poly1305Async(string ciphertextBase64, byte[] key, string? contextBase64)
    {
        var combined = Convert.FromBase64String(ciphertextBase64);
        var additionalData = contextBase64 != null ? Convert.FromBase64String(contextBase64) : null;

        var nonce = new byte[12];
        var tag = new byte[16];
        var ciphertext = new byte[combined.Length - 28];

        Buffer.BlockCopy(combined, 0, nonce, 0, 12);
        Buffer.BlockCopy(combined, 12, tag, 0, 16);
        Buffer.BlockCopy(combined, 28, ciphertext, 0, ciphertext.Length);

        using var chacha = new ChaCha20Poly1305(key);
        var plaintext = new byte[ciphertext.Length];

        chacha.Decrypt(nonce, ciphertext, tag, plaintext, additionalData);

        return Convert.ToBase64String(plaintext);
    }

    private (int version, string ciphertext) ParseVaultCiphertext(string vaultCiphertext)
    {
        // Format: vault:v{version}:{ciphertext}
        var parts = vaultCiphertext.Split(':', 3);
        if (parts.Length != 3 || parts[0] != "vault" || !parts[1].StartsWith("v"))
            throw new ArgumentException("Invalid vault ciphertext format. Expected: vault:v{version}:{ciphertext}");

        if (!int.TryParse(parts[1].Substring(1), out var version))
            throw new ArgumentException("Invalid version in vault ciphertext");

        return (version, parts[2]);
    }

    private async Task<string> SignDataAsync(string inputBase64, byte[] privateKey, string keyType, string hashAlgorithm)
    {
        var data = Convert.FromBase64String(inputBase64);
        var hashAlg = hashAlgorithm == "sha2-512" ? HashAlgorithmName.SHA512 : HashAlgorithmName.SHA256;

        if (keyType.StartsWith("rsa-"))
        {
            using var rsa = RSA.Create();
            rsa.ImportRSAPrivateKey(privateKey, out _);
            var signature = rsa.SignData(data, hashAlg, RSASignaturePadding.Pkcs1);
            return Convert.ToBase64String(signature);
        }
        else if (keyType == "ecdsa-p256")
        {
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportECPrivateKey(privateKey, out _);
            var signature = ecdsa.SignData(data, hashAlg);
            return Convert.ToBase64String(signature);
        }

        throw new NotSupportedException($"Signing not implemented for key type '{keyType}'");
    }

    private async Task<bool> VerifySignatureAsync(string inputBase64, string signatureBase64, string publicKeyBase64, string keyType, string hashAlgorithm)
    {
        var data = Convert.FromBase64String(inputBase64);
        var signature = Convert.FromBase64String(signatureBase64);
        var hashAlg = hashAlgorithm == "sha2-512" ? HashAlgorithmName.SHA512 : HashAlgorithmName.SHA256;

        try
        {
            if (keyType.StartsWith("rsa-"))
            {
                using var rsa = RSA.Create();
                rsa.ImportRSAPublicKey(Convert.FromBase64String(publicKeyBase64), out _);
                return rsa.VerifyData(data, signature, hashAlg, RSASignaturePadding.Pkcs1);
            }
            else if (keyType == "ecdsa-p256")
            {
                using var ecdsa = ECDsa.Create();
                ecdsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKeyBase64), out _);
                return ecdsa.VerifyData(data, signature, hashAlg);
            }

            return false;
        }
        catch
        {
            return false;
        }
    }
}
