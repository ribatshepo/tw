using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using USP.Core.Domain.Entities.Secrets;
using USP.Core.Exceptions;
using USP.Core.Interfaces.Services.Secrets;
using USP.Infrastructure.Metrics;
using USP.Infrastructure.Persistence;

namespace USP.Infrastructure.Services.Secrets;

/// <summary>
/// Implements the Transit Encryption Engine for encryption-as-a-service.
/// </summary>
public class TransitEngine : ITransitEngine
{
    private readonly ApplicationDbContext _context;
    private readonly ISealService _sealService;
    private readonly ILogger<TransitEngine> _logger;

    public TransitEngine(
        ApplicationDbContext context,
        ISealService sealService,
        ILogger<TransitEngine> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _sealService = sealService ?? throw new ArgumentNullException(nameof(sealService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task CreateKeyAsync(
        string name,
        string keyType = "aes256-gcm96",
        bool derivation = false,
        bool exportable = false,
        CancellationToken cancellationToken = default)
    {
        // Check if vault is sealed
        if (_sealService.IsSealed())
        {
            throw new VaultSealedException();
        }

        _logger.LogInformation("Creating transit key: {KeyName} ({KeyType})", name, keyType);

        // Check if key already exists
        var existing = await _context.TransitKeys
            .FirstOrDefaultAsync(k => k.Name == name, cancellationToken);

        if (existing != null)
        {
            throw new InvalidOperationException($"Transit key '{name}' already exists");
        }

        // Validate key type and get capabilities
        var (supportsEncryption, supportsSigning, supportsDerivation) = GetKeyCapabilities(keyType);

        // Generate initial key material
        var keyMaterial = GenerateKeyMaterial(keyType);

        // Encrypt key material with master key
        var encryptedKeyMaterial = EncryptKeyMaterial(keyMaterial, _sealService.GetMasterKey()!);

        // Create key entity
        var transitKey = new TransitKey
        {
            Name = name,
            Type = keyType,
            Derivation = derivation && supportsDerivation,
            Exportable = exportable,
            DeletionAllowed = true,
            LatestVersion = 1,
            MinDecryptionVersion = 1,
            MinEncryptionVersion = 1,
            SupportsEncryption = supportsEncryption,
            SupportsSigning = supportsSigning,
            SupportsDerivation = supportsDerivation,
            KeyVersionsJson = JsonSerializer.Serialize(new Dictionary<int, DateTime> { { 1, DateTime.UtcNow } }),
            EncryptedKeyMaterialJson = JsonSerializer.Serialize(new Dictionary<int, string> { { 1, encryptedKeyMaterial } })
        };

        _context.TransitKeys.Add(transitKey);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Transit key created: {KeyName} (type: {KeyType}, encryption: {Encryption}, signing: {Signing})",
            name, keyType, supportsEncryption, supportsSigning);
    }

    public async Task<TransitEncryptResult> EncryptAsync(
        string keyName,
        string plaintext,
        string? context = null,
        int? keyVersion = null,
        CancellationToken cancellationToken = default)
    {
        // Check if vault is sealed
        if (_sealService.IsSealed())
        {
            throw new VaultSealedException();
        }


        var key = await _context.TransitKeys
            .FirstOrDefaultAsync(k => k.Name == keyName, cancellationToken)
            ?? throw new InvalidOperationException($"Transit key '{keyName}' not found");

        if (!key.SupportsEncryption)
        {
            throw new InvalidOperationException($"Transit key '{keyName}' does not support encryption");
        }

        // Use specified version or latest
        var version = keyVersion ?? key.LatestVersion;

        if (version < key.MinEncryptionVersion)
        {
            throw new InvalidOperationException(
                $"Key version {version} is below minimum encryption version {key.MinEncryptionVersion}");
        }

        // Get key material for this version
        var keyMaterialDict = JsonSerializer.Deserialize<Dictionary<int, string>>(key.EncryptedKeyMaterialJson)!;
        if (!keyMaterialDict.TryGetValue(version, out var encryptedKeyMaterial))
        {
            throw new InvalidOperationException($"Key version {version} not found");
        }

        // Decrypt key material
        var keyMaterial = DecryptKeyMaterial(encryptedKeyMaterial, _sealService.GetMasterKey()!);

        // Derive key if derivation is enabled
        byte[] actualKey = keyMaterial;
        if (key.Derivation && !string.IsNullOrEmpty(context))
        {
            var contextBytes = Convert.FromBase64String(context);
            actualKey = DeriveKey(keyMaterial, contextBytes);
        }

        // Decrypt plaintext from base64
        var plaintextBytes = Convert.FromBase64String(plaintext);

        // Encrypt based on key type
        string ciphertext = key.Type switch
        {
            "aes256-gcm96" => EncryptAesGcm(plaintextBytes, actualKey, version),
            "chacha20-poly1305" => EncryptChaCha20Poly1305(plaintextBytes, actualKey, version),
            _ => throw new NotSupportedException($"Encryption not supported for key type {key.Type}")
        };

        // Record metric
        SecurityMetrics.RecordSecretOperation("encrypt", "transit");

        _logger.LogInformation("Data encrypted with transit key: {KeyName} (version {Version})", keyName, version);

        return new TransitEncryptResult
        {
            Ciphertext = ciphertext,
            KeyVersion = version
        };
    }

    public async Task<TransitDecryptResult> DecryptAsync(
        string keyName,
        string ciphertext,
        string? context = null,
        CancellationToken cancellationToken = default)
    {
        // Check if vault is sealed
        if (_sealService.IsSealed())
        {
            throw new VaultSealedException();
        }


        var key = await _context.TransitKeys
            .FirstOrDefaultAsync(k => k.Name == keyName, cancellationToken)
            ?? throw new InvalidOperationException($"Transit key '{keyName}' not found");

        if (!key.SupportsEncryption)
        {
            throw new InvalidOperationException($"Transit key '{keyName}' does not support decryption");
        }

        // Parse ciphertext: vault:v{version}:{base64-data}
        var parts = ciphertext.Split(':', 3);
        if (parts.Length != 3 || parts[0] != "vault" || !parts[1].StartsWith("v"))
        {
            throw new InvalidOperationException("Invalid ciphertext format");
        }

        var version = int.Parse(parts[1].Substring(1));

        if (version < key.MinDecryptionVersion)
        {
            throw new InvalidOperationException(
                $"Key version {version} is below minimum decryption version {key.MinDecryptionVersion}");
        }

        // Get key material for this version
        var keyMaterialDict = JsonSerializer.Deserialize<Dictionary<int, string>>(key.EncryptedKeyMaterialJson)!;
        if (!keyMaterialDict.TryGetValue(version, out var encryptedKeyMaterial))
        {
            throw new InvalidOperationException($"Key version {version} not found");
        }

        // Decrypt key material
        var keyMaterial = DecryptKeyMaterial(encryptedKeyMaterial, _sealService.GetMasterKey()!);

        // Derive key if derivation is enabled
        byte[] actualKey = keyMaterial;
        if (key.Derivation && !string.IsNullOrEmpty(context))
        {
            var contextBytes = Convert.FromBase64String(context);
            actualKey = DeriveKey(keyMaterial, contextBytes);
        }

        // Decrypt based on key type
        byte[] plaintextBytes = key.Type switch
        {
            "aes256-gcm96" => DecryptAesGcm(parts[2], actualKey),
            "chacha20-poly1305" => DecryptChaCha20Poly1305(parts[2], actualKey),
            _ => throw new NotSupportedException($"Decryption not supported for key type {key.Type}")
        };

        // Encode plaintext to base64
        var plaintext = Convert.ToBase64String(plaintextBytes);

        // Record metric
        SecurityMetrics.RecordSecretOperation("decrypt", "transit");

        _logger.LogInformation("Data decrypted with transit key: {KeyName} (version {Version})", keyName, version);

        return new TransitDecryptResult
        {
            Plaintext = plaintext
        };
    }

    public async Task RotateKeyAsync(
        string keyName,
        CancellationToken cancellationToken = default)
    {
        // Check if vault is sealed
        if (_sealService.IsSealed())
        {
            throw new VaultSealedException();
        }

        _logger.LogInformation("Rotating transit key: {KeyName}", keyName);

        var key = await _context.TransitKeys
            .FirstOrDefaultAsync(k => k.Name == keyName, cancellationToken)
            ?? throw new InvalidOperationException($"Transit key '{keyName}' not found");

        // Generate new key material
        var newKeyMaterial = GenerateKeyMaterial(key.Type);
        var encryptedNewKeyMaterial = EncryptKeyMaterial(newKeyMaterial, _sealService.GetMasterKey()!);

        // Increment version
        var newVersion = key.LatestVersion + 1;

        // Update key versions
        var keyVersions = JsonSerializer.Deserialize<Dictionary<int, DateTime>>(key.KeyVersionsJson)!;
        keyVersions[newVersion] = DateTime.UtcNow;

        var keyMaterials = JsonSerializer.Deserialize<Dictionary<int, string>>(key.EncryptedKeyMaterialJson)!;
        keyMaterials[newVersion] = encryptedNewKeyMaterial;

        key.LatestVersion = newVersion;
        key.MinEncryptionVersion = newVersion; // Force new encryptions to use new version
        key.KeyVersionsJson = JsonSerializer.Serialize(keyVersions);
        key.EncryptedKeyMaterialJson = JsonSerializer.Serialize(keyMaterials);
        key.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Transit key rotated: {KeyName} (new version: {Version})", keyName, newVersion);
    }

    public async Task<TransitRewrapResult> RewrapAsync(
        string keyName,
        string ciphertext,
        string? context = null,
        CancellationToken cancellationToken = default)
    {
        // Decrypt with old version
        var decryptResult = await DecryptAsync(keyName, ciphertext, context, cancellationToken);

        // Encrypt with latest version
        var encryptResult = await EncryptAsync(keyName, decryptResult.Plaintext, context, null, cancellationToken);

        return new TransitRewrapResult
        {
            Ciphertext = encryptResult.Ciphertext,
            KeyVersion = encryptResult.KeyVersion
        };
    }

    public async Task<TransitKeyInfo?> ReadKeyAsync(
        string keyName,
        CancellationToken cancellationToken = default)
    {
        var key = await _context.TransitKeys
            .FirstOrDefaultAsync(k => k.Name == keyName, cancellationToken);

        if (key == null)
        {
            return null;
        }

        var keyVersions = JsonSerializer.Deserialize<Dictionary<int, DateTime>>(key.KeyVersionsJson)!;

        return new TransitKeyInfo
        {
            Name = key.Name,
            Type = key.Type,
            Derivation = key.Derivation,
            Exportable = key.Exportable,
            DeletionAllowed = key.DeletionAllowed,
            LatestVersion = key.LatestVersion,
            MinDecryptionVersion = key.MinDecryptionVersion,
            MinEncryptionVersion = key.MinEncryptionVersion,
            Keys = keyVersions,
            CreatedAt = key.CreatedAt
        };
    }

    public async Task<List<string>> ListKeysAsync(CancellationToken cancellationToken = default)
    {
        return await _context.TransitKeys
            .Select(k => k.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task DeleteKeyAsync(
        string keyName,
        CancellationToken cancellationToken = default)
    {
        var key = await _context.TransitKeys
            .FirstOrDefaultAsync(k => k.Name == keyName, cancellationToken)
            ?? throw new InvalidOperationException($"Transit key '{keyName}' not found");

        if (!key.DeletionAllowed)
        {
            throw new InvalidOperationException($"Transit key '{keyName}' cannot be deleted");
        }

        key.DeletedAt = DateTime.UtcNow;
        key.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Transit key deleted: {KeyName}", keyName);
    }

    // Helper methods for key operations

    private (bool encryption, bool signing, bool derivation) GetKeyCapabilities(string keyType)
    {
        return keyType switch
        {
            "aes256-gcm96" => (true, false, true),
            "chacha20-poly1305" => (true, false, true),
            "rsa-2048" => (true, true, false),
            "rsa-4096" => (true, true, false),
            "ecdsa-p256" => (false, true, false),
            "ecdsa-p384" => (false, true, false),
            "ecdsa-p521" => (false, true, false),
            "ed25519" => (false, true, false),
            _ => throw new ArgumentException($"Unsupported key type: {keyType}")
        };
    }

    private byte[] GenerateKeyMaterial(string keyType)
    {
        return keyType switch
        {
            "aes256-gcm96" => RandomNumberGenerator.GetBytes(32), // 256 bits
            "chacha20-poly1305" => RandomNumberGenerator.GetBytes(32), // 256 bits
            "rsa-2048" => GenerateRsaKey(2048),
            "rsa-4096" => GenerateRsaKey(4096),
            "ecdsa-p256" => GenerateEcdsaKey(256),
            "ecdsa-p384" => GenerateEcdsaKey(384),
            "ecdsa-p521" => GenerateEcdsaKey(521),
            "ed25519" => RandomNumberGenerator.GetBytes(32),
            _ => throw new ArgumentException($"Unsupported key type: {keyType}")
        };
    }

    private byte[] GenerateRsaKey(int keySize)
    {
        using var rsa = RSA.Create(keySize);
        return rsa.ExportRSAPrivateKey();
    }

    private byte[] GenerateEcdsaKey(int keySize)
    {
        var curve = keySize switch
        {
            256 => ECCurve.NamedCurves.nistP256,
            384 => ECCurve.NamedCurves.nistP384,
            521 => ECCurve.NamedCurves.nistP521,
            _ => throw new ArgumentException($"Unsupported ECDSA key size: {keySize}")
        };

        using var ecdsa = ECDsa.Create(curve);
        return ecdsa.ExportECPrivateKey();
    }

    private string EncryptKeyMaterial(byte[] keyMaterial, byte[] masterKey)
    {
        using var aes = Aes.Create();
        aes.Key = masterKey;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        var ciphertext = encryptor.TransformFinalBlock(keyMaterial, 0, keyMaterial.Length);

        // Return IV + ciphertext as base64
        var result = new byte[aes.IV.Length + ciphertext.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
        Buffer.BlockCopy(ciphertext, 0, result, aes.IV.Length, ciphertext.Length);

        return Convert.ToBase64String(result);
    }

    private byte[] DecryptKeyMaterial(string encryptedKeyMaterial, byte[] masterKey)
    {
        var data = Convert.FromBase64String(encryptedKeyMaterial);

        using var aes = Aes.Create();
        aes.Key = masterKey;

        // Extract IV
        var iv = new byte[16];
        Buffer.BlockCopy(data, 0, iv, 0, 16);
        aes.IV = iv;

        // Extract ciphertext
        var ciphertext = new byte[data.Length - 16];
        Buffer.BlockCopy(data, 16, ciphertext, 0, ciphertext.Length);

        using var decryptor = aes.CreateDecryptor();
        return decryptor.TransformFinalBlock(ciphertext, 0, ciphertext.Length);
    }

    private byte[] DeriveKey(byte[] baseKey, byte[] context)
    {
        // Use HKDF for key derivation
        using var hmac = new HMACSHA256(baseKey);
        var derivedKey = hmac.ComputeHash(context);
        return derivedKey;
    }

    private string EncryptAesGcm(byte[] plaintext, byte[] key, int version)
    {
        var nonce = RandomNumberGenerator.GetBytes(12); // 96 bits for GCM
        var tag = new byte[16]; // 128-bit authentication tag
        var ciphertext = new byte[plaintext.Length];

        using var aesGcm = new AesGcm(key, 16);
        aesGcm.Encrypt(nonce, plaintext, ciphertext, tag);

        // Combine nonce + tag + ciphertext
        var combined = new byte[nonce.Length + tag.Length + ciphertext.Length];
        Buffer.BlockCopy(nonce, 0, combined, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, combined, nonce.Length, tag.Length);
        Buffer.BlockCopy(ciphertext, 0, combined, nonce.Length + tag.Length, ciphertext.Length);

        return $"vault:v{version}:{Convert.ToBase64String(combined)}";
    }

    private byte[] DecryptAesGcm(string base64Data, byte[] key)
    {
        var combined = Convert.FromBase64String(base64Data);

        // Extract nonce, tag, ciphertext
        var nonce = new byte[12];
        var tag = new byte[16];
        var ciphertext = new byte[combined.Length - 28];

        Buffer.BlockCopy(combined, 0, nonce, 0, 12);
        Buffer.BlockCopy(combined, 12, tag, 0, 16);
        Buffer.BlockCopy(combined, 28, ciphertext, 0, ciphertext.Length);

        var plaintext = new byte[ciphertext.Length];

        using var aesGcm = new AesGcm(key, 16);
        aesGcm.Decrypt(nonce, ciphertext, tag, plaintext);

        return plaintext;
    }

    private string EncryptChaCha20Poly1305(byte[] plaintext, byte[] key, int version)
    {
        var nonce = RandomNumberGenerator.GetBytes(12); // 96 bits
        var tag = new byte[16]; // 128-bit authentication tag
        var ciphertext = new byte[plaintext.Length];

        using var cipher = new ChaCha20Poly1305(key);
        cipher.Encrypt(nonce, plaintext, ciphertext, tag);

        // Combine nonce + tag + ciphertext
        var combined = new byte[nonce.Length + tag.Length + ciphertext.Length];
        Buffer.BlockCopy(nonce, 0, combined, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, combined, nonce.Length, tag.Length);
        Buffer.BlockCopy(ciphertext, 0, combined, nonce.Length + tag.Length, ciphertext.Length);

        return $"vault:v{version}:{Convert.ToBase64String(combined)}";
    }

    private byte[] DecryptChaCha20Poly1305(string base64Data, byte[] key)
    {
        var combined = Convert.FromBase64String(base64Data);

        // Extract nonce, tag, ciphertext
        var nonce = new byte[12];
        var tag = new byte[16];
        var ciphertext = new byte[combined.Length - 28];

        Buffer.BlockCopy(combined, 0, nonce, 0, 12);
        Buffer.BlockCopy(combined, 12, tag, 0, 16);
        Buffer.BlockCopy(combined, 28, ciphertext, 0, ciphertext.Length);

        var plaintext = new byte[ciphertext.Length];

        using var cipher = new ChaCha20Poly1305(key);
        cipher.Decrypt(nonce, ciphertext, tag, plaintext);

        return plaintext;
    }

    public async Task<TransitSignResult> SignAsync(
        string keyName,
        string input,
        string hashAlgorithm = "sha2-256",
        string? context = null,
        CancellationToken cancellationToken = default)
    {
        // Check if vault is sealed
        if (_sealService.IsSealed())
        {
            throw new VaultSealedException();
        }


        var key = await _context.TransitKeys
            .FirstOrDefaultAsync(k => k.Name == keyName, cancellationToken)
            ?? throw new InvalidOperationException($"Transit key '{keyName}' not found");

        if (!key.SupportsSigning)
        {
            throw new InvalidOperationException($"Transit key '{keyName}' does not support signing");
        }

        var version = key.LatestVersion;

        // Get key material
        var keyMaterialDict = JsonSerializer.Deserialize<Dictionary<int, string>>(key.EncryptedKeyMaterialJson)!;
        var keyMaterial = DecryptKeyMaterial(keyMaterialDict[version], _sealService.GetMasterKey()!);

        // Decode input
        var inputBytes = Convert.FromBase64String(input);

        // Sign based on key type
        var signature = key.Type switch
        {
            "rsa-2048" or "rsa-4096" => SignRsa(inputBytes, keyMaterial, hashAlgorithm),
            "ecdsa-p256" => SignEcdsa(inputBytes, keyMaterial, 256, hashAlgorithm),
            "ecdsa-p384" => SignEcdsa(inputBytes, keyMaterial, 384, hashAlgorithm),
            "ecdsa-p521" => SignEcdsa(inputBytes, keyMaterial, 521, hashAlgorithm),
            "ed25519" => SignEd25519(inputBytes, keyMaterial),
            _ => throw new NotSupportedException($"Signing not supported for key type {key.Type}")
        };

        // Record metric
        SecurityMetrics.RecordSecretOperation("sign", "transit");

        _logger.LogInformation("Data signed with transit key: {KeyName} (version {Version})", keyName, version);

        return new TransitSignResult
        {
            Signature = $"vault:v{version}:{hashAlgorithm}:{signature}",
            KeyVersion = version
        };
    }

    public async Task<TransitVerifyResult> VerifyAsync(
        string keyName,
        string input,
        string signature,
        string hashAlgorithm = "sha2-256",
        string? context = null,
        CancellationToken cancellationToken = default)
    {
        // Check if vault is sealed
        if (_sealService.IsSealed())
        {
            throw new VaultSealedException();
        }


        var key = await _context.TransitKeys
            .FirstOrDefaultAsync(k => k.Name == keyName, cancellationToken)
            ?? throw new InvalidOperationException($"Transit key '{keyName}' not found");

        if (!key.SupportsSigning)
        {
            throw new InvalidOperationException($"Transit key '{keyName}' does not support verification");
        }

        // Parse signature: vault:v{version}:{algorithm}:{base64-signature}
        var parts = signature.Split(':', 4);
        if (parts.Length != 4 || parts[0] != "vault" || !parts[1].StartsWith("v"))
        {
            throw new InvalidOperationException("Invalid signature format");
        }

        var version = int.Parse(parts[1].Substring(1));
        var signatureBytes = Convert.FromBase64String(parts[3]);

        // Get key material
        var keyMaterialDict = JsonSerializer.Deserialize<Dictionary<int, string>>(key.EncryptedKeyMaterialJson)!;
        var keyMaterial = DecryptKeyMaterial(keyMaterialDict[version], _sealService.GetMasterKey()!);

        // Decode input
        var inputBytes = Convert.FromBase64String(input);

        // Verify based on key type
        var isValid = key.Type switch
        {
            "rsa-2048" or "rsa-4096" => VerifyRsa(inputBytes, signatureBytes, keyMaterial, hashAlgorithm),
            "ecdsa-p256" => VerifyEcdsa(inputBytes, signatureBytes, keyMaterial, 256, hashAlgorithm),
            "ecdsa-p384" => VerifyEcdsa(inputBytes, signatureBytes, keyMaterial, 384, hashAlgorithm),
            "ecdsa-p521" => VerifyEcdsa(inputBytes, signatureBytes, keyMaterial, 521, hashAlgorithm),
            "ed25519" => VerifyEd25519(inputBytes, signatureBytes, keyMaterial),
            _ => throw new NotSupportedException($"Verification not supported for key type {key.Type}")
        };

        // Record metric
        SecurityMetrics.RecordSecretOperation("verify", "transit");

        _logger.LogInformation("Signature verified with transit key: {KeyName} (version {Version}, valid: {Valid})",
            keyName, version, isValid);

        return new TransitVerifyResult
        {
            Valid = isValid
        };
    }

    public async Task<TransitHmacResult> GenerateHmacAsync(
        string keyName,
        string input,
        string hashAlgorithm = "sha2-256",
        CancellationToken cancellationToken = default)
    {
        // Check if vault is sealed
        if (_sealService.IsSealed())
        {
            throw new VaultSealedException();
        }


        var key = await _context.TransitKeys
            .FirstOrDefaultAsync(k => k.Name == keyName, cancellationToken)
            ?? throw new InvalidOperationException($"Transit key '{keyName}' not found");

        var version = key.LatestVersion;

        // Get key material
        var keyMaterialDict = JsonSerializer.Deserialize<Dictionary<int, string>>(key.EncryptedKeyMaterialJson)!;
        var keyMaterial = DecryptKeyMaterial(keyMaterialDict[version], _sealService.GetMasterKey()!);

        // Decode input
        var inputBytes = Convert.FromBase64String(input);

        // Generate HMAC
        var hmacBytes = hashAlgorithm switch
        {
            "sha2-256" => HMACSHA256.HashData(keyMaterial, inputBytes),
            "sha2-512" => HMACSHA512.HashData(keyMaterial, inputBytes),
            _ => throw new NotSupportedException($"Unsupported hash algorithm: {hashAlgorithm}")
        };

        var hmac = Convert.ToBase64String(hmacBytes);

        // Record metric
        SecurityMetrics.RecordSecretOperation("hmac", "transit");

        _logger.LogInformation("HMAC generated with transit key: {KeyName} (version {Version})", keyName, version);

        return new TransitHmacResult
        {
            Hmac = $"vault:v{version}:{hashAlgorithm}:{hmac}",
            KeyVersion = version
        };
    }

    // Signing/verification helper methods

    private string SignRsa(byte[] data, byte[] privateKeyBytes, string hashAlgorithm)
    {
        using var rsa = RSA.Create();
        rsa.ImportRSAPrivateKey(privateKeyBytes, out _);

        var hashName = hashAlgorithm switch
        {
            "sha2-256" => HashAlgorithmName.SHA256,
            "sha2-512" => HashAlgorithmName.SHA512,
            _ => throw new NotSupportedException($"Unsupported hash algorithm: {hashAlgorithm}")
        };

        var signature = rsa.SignData(data, hashName, RSASignaturePadding.Pkcs1);
        return Convert.ToBase64String(signature);
    }

    private bool VerifyRsa(byte[] data, byte[] signature, byte[] privateKeyBytes, string hashAlgorithm)
    {
        using var rsa = RSA.Create();
        rsa.ImportRSAPrivateKey(privateKeyBytes, out _);

        var hashName = hashAlgorithm switch
        {
            "sha2-256" => HashAlgorithmName.SHA256,
            "sha2-512" => HashAlgorithmName.SHA512,
            _ => throw new NotSupportedException($"Unsupported hash algorithm: {hashAlgorithm}")
        };

        return rsa.VerifyData(data, signature, hashName, RSASignaturePadding.Pkcs1);
    }

    private string SignEcdsa(byte[] data, byte[] privateKeyBytes, int keySize, string hashAlgorithm)
    {
        var curve = keySize switch
        {
            256 => ECCurve.NamedCurves.nistP256,
            384 => ECCurve.NamedCurves.nistP384,
            521 => ECCurve.NamedCurves.nistP521,
            _ => throw new ArgumentException($"Unsupported ECDSA key size: {keySize}")
        };

        using var ecdsa = ECDsa.Create(curve);
        ecdsa.ImportECPrivateKey(privateKeyBytes, out _);

        var hashName = hashAlgorithm switch
        {
            "sha2-256" => HashAlgorithmName.SHA256,
            "sha2-512" => HashAlgorithmName.SHA512,
            _ => throw new NotSupportedException($"Unsupported hash algorithm: {hashAlgorithm}")
        };

        var signature = ecdsa.SignData(data, hashName);
        return Convert.ToBase64String(signature);
    }

    private bool VerifyEcdsa(byte[] data, byte[] signature, byte[] privateKeyBytes, int keySize, string hashAlgorithm)
    {
        var curve = keySize switch
        {
            256 => ECCurve.NamedCurves.nistP256,
            384 => ECCurve.NamedCurves.nistP384,
            521 => ECCurve.NamedCurves.nistP521,
            _ => throw new ArgumentException($"Unsupported ECDSA key size: {keySize}")
        };

        using var ecdsa = ECDsa.Create(curve);
        ecdsa.ImportECPrivateKey(privateKeyBytes, out _);

        var hashName = hashAlgorithm switch
        {
            "sha2-256" => HashAlgorithmName.SHA256,
            "sha2-512" => HashAlgorithmName.SHA512,
            _ => throw new NotSupportedException($"Unsupported hash algorithm: {hashAlgorithm}")
        };

        return ecdsa.VerifyData(data, signature, hashName);
    }

    private string SignEd25519(byte[] data, byte[] privateKeyBytes)
    {
        using var key = NSec.Cryptography.Key.Import(
            NSec.Cryptography.SignatureAlgorithm.Ed25519,
            privateKeyBytes,
            NSec.Cryptography.KeyBlobFormat.RawPrivateKey);

        var signature = NSec.Cryptography.SignatureAlgorithm.Ed25519.Sign(key, data);
        return Convert.ToBase64String(signature);
    }

    private bool VerifyEd25519(byte[] data, byte[] signature, byte[] privateKeyBytes)
    {
        using var key = NSec.Cryptography.Key.Import(
            NSec.Cryptography.SignatureAlgorithm.Ed25519,
            privateKeyBytes,
            NSec.Cryptography.KeyBlobFormat.RawPrivateKey);

        var publicKey = NSec.Cryptography.PublicKey.Import(
            NSec.Cryptography.SignatureAlgorithm.Ed25519,
            key.Export(NSec.Cryptography.KeyBlobFormat.RawPublicKey),
            NSec.Cryptography.KeyBlobFormat.RawPublicKey);

        return NSec.Cryptography.SignatureAlgorithm.Ed25519.Verify(publicKey, data, signature);
    }
}
