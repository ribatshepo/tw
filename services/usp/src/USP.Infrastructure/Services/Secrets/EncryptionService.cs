using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using USP.Core.Domain.Entities.Secrets;
using USP.Core.Domain.Enums;
using USP.Core.Exceptions;
using USP.Core.Interfaces.Services.Secrets;
using USP.Infrastructure.Persistence;

namespace USP.Infrastructure.Services.Secrets;

/// <summary>
/// Implementation of encryption service using AES-256-GCM
/// </summary>
public class EncryptionService : IEncryptionService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<EncryptionService> _logger;
    private readonly IMasterKeyProvider _masterKeyProvider;
    private readonly ISealService _sealService;

    public EncryptionService(
        ApplicationDbContext context,
        ILogger<EncryptionService> logger,
        IMasterKeyProvider masterKeyProvider,
        ISealService sealService)
    {
        _context = context;
        _logger = logger;
        _masterKeyProvider = masterKeyProvider;
        _sealService = sealService;

        _logger.LogInformation(
            "EncryptionService initialized with master key version: {KeyVersion}",
            _masterKeyProvider.GetKeyVersion());
    }

    public async Task<EncryptionKey> CreateKeyAsync(
        string name,
        EncryptionAlgorithm algorithm = EncryptionAlgorithm.AES256GCM,
        bool exportable = false,
        CancellationToken cancellationToken = default)
    {
        var existingKey = await _context.Set<EncryptionKey>()
            .FirstOrDefaultAsync(k => k.Name == name && k.DeletedAt == null, cancellationToken);

        if (existingKey != null)
        {
            throw new InvalidOperationException($"Encryption key '{name}' already exists");
        }

        var key = new EncryptionKey
        {
            Name = name,
            Algorithm = algorithm,
            CurrentVersion = 1,
            MinDecryptionVersion = 1,
            Exportable = exportable,
            DeletionAllowed = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Set<EncryptionKey>().Add(key);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Encryption key created: {KeyName} ({Algorithm})", name, algorithm);

        return key;
    }

    public async Task<EncryptionKey?> ReadKeyAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        return await _context.Set<EncryptionKey>()
            .FirstOrDefaultAsync(k => k.Name == name && k.DeletedAt == null, cancellationToken);
    }

    public async Task<List<string>> ListKeysAsync(
        CancellationToken cancellationToken = default)
    {
        return await _context.Set<EncryptionKey>()
            .Where(k => k.DeletedAt == null)
            .Select(k => k.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task DeleteKeyAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        var key = await _context.Set<EncryptionKey>()
            .FirstOrDefaultAsync(k => k.Name == name && k.DeletedAt == null, cancellationToken)
            ?? throw new InvalidOperationException($"Encryption key '{name}' not found");

        if (!key.DeletionAllowed)
        {
            throw new InvalidOperationException($"Encryption key '{name}' cannot be deleted");
        }

        key.DeletedAt = DateTime.UtcNow;
        key.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Encryption key deleted: {KeyName}", name);
    }

    public async Task<EncryptionKey> RotateKeyAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        var key = await _context.Set<EncryptionKey>()
            .FirstOrDefaultAsync(k => k.Name == name && k.DeletedAt == null, cancellationToken)
            ?? throw new InvalidOperationException($"Encryption key '{name}' not found");

        key.CurrentVersion++;
        key.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Encryption key rotated: {KeyName} (version {Version})", name, key.CurrentVersion);

        return key;
    }

    public async Task<string> EncryptAsync(
        string keyName,
        string plaintext,
        string? context = null,
        CancellationToken cancellationToken = default)
    {
        // Check if vault is sealed
        if (_sealService.IsSealed())
        {
            _logger.LogWarning("Attempted to encrypt data while vault is sealed");
            throw new VaultSealedException();
        }

        var key = await _context.Set<EncryptionKey>()
            .FirstOrDefaultAsync(k => k.Name == keyName && k.DeletedAt == null, cancellationToken)
            ?? throw new InvalidOperationException($"Encryption key '{keyName}' not found");

        if (key.Algorithm != EncryptionAlgorithm.AES256GCM)
        {
            throw new NotSupportedException(
                $"Algorithm {key.Algorithm} is not supported. " +
                "Currently only AES-256-GCM is supported. " +
                "Support for additional algorithms can be added as needed.");
        }

        // Encrypt using AES-256-GCM
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var (ciphertext, nonce, tag) = EncryptAesGcm(plaintextBytes, _masterKeyProvider.GetMasterKey());

        // Format: vault:v{version}:{base64(nonce)}:{base64(tag)}:{base64(ciphertext)}
        var result = $"vault:v{key.CurrentVersion}:{Convert.ToBase64String(nonce)}:{Convert.ToBase64String(tag)}:{Convert.ToBase64String(ciphertext)}";

        _logger.LogInformation("Data encrypted with key: {KeyName} (version {Version})", keyName, key.CurrentVersion);

        return result;
    }

    public async Task<string> DecryptAsync(
        string keyName,
        string ciphertext,
        string? context = null,
        CancellationToken cancellationToken = default)
    {
        // Check if vault is sealed
        if (_sealService.IsSealed())
        {
            _logger.LogWarning("Attempted to decrypt data while vault is sealed");
            throw new VaultSealedException();
        }

        var key = await _context.Set<EncryptionKey>()
            .FirstOrDefaultAsync(k => k.Name == keyName && k.DeletedAt == null, cancellationToken)
            ?? throw new InvalidOperationException($"Encryption key '{keyName}' not found");

        // Parse: vault:v{version}:{base64(nonce)}:{base64(tag)}:{base64(ciphertext)}
        var parts = ciphertext.Split(':');
        if (parts.Length != 5 || parts[0] != "vault")
        {
            throw new InvalidOperationException("Invalid ciphertext format");
        }

        var version = int.Parse(parts[1].Substring(1)); // Remove 'v' prefix
        var nonce = Convert.FromBase64String(parts[2]);
        var tag = Convert.FromBase64String(parts[3]);
        var encryptedData = Convert.FromBase64String(parts[4]);

        if (version < key.MinDecryptionVersion)
        {
            throw new InvalidOperationException($"Version {version} is below minimum decryption version {key.MinDecryptionVersion}");
        }

        // Decrypt using AES-256-GCM
        var plaintextBytes = DecryptAesGcm(encryptedData, _masterKeyProvider.GetMasterKey(), nonce, tag);
        var plaintext = Encoding.UTF8.GetString(plaintextBytes);

        _logger.LogInformation("Data decrypted with key: {KeyName} (version {Version})", keyName, version);

        return plaintext;
    }

    public async Task<string> RewrapAsync(
        string keyName,
        string ciphertext,
        string? context = null,
        CancellationToken cancellationToken = default)
    {
        // Decrypt with old version, encrypt with new version
        var plaintext = await DecryptAsync(keyName, ciphertext, context, cancellationToken);
        return await EncryptAsync(keyName, plaintext, context, cancellationToken);
    }

    public Task<byte[]> GenerateRandomBytesAsync(
        int length,
        CancellationToken cancellationToken = default)
    {
        var bytes = new byte[length];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(bytes);
        }
        return Task.FromResult(bytes);
    }

    public Task<string> HashAsync(
        string data,
        string algorithm = "sha2-256",
        CancellationToken cancellationToken = default)
    {
        byte[] hash;
        var dataBytes = Encoding.UTF8.GetBytes(data);

        switch (algorithm)
        {
            case "sha2-256":
                hash = SHA256.HashData(dataBytes);
                break;
            case "sha2-512":
                hash = SHA512.HashData(dataBytes);
                break;
            case "sha3-256":
            case "sha3-512":
                throw new NotSupportedException(
                    $"Algorithm {algorithm} is not currently supported. " +
                    "SHA-3 support requires .NET 9+ or external library (BouncyCastle). " +
                    "Supported algorithms: sha2-256, sha2-512");
            default:
                throw new ArgumentException($"Unknown hash algorithm: {algorithm}");
        }

        return Task.FromResult(Convert.ToHexString(hash).ToLowerInvariant());
    }

    // Private helper methods for AES-256-GCM

    private static (byte[] ciphertext, byte[] nonce, byte[] tag) EncryptAesGcm(byte[] plaintext, byte[] key)
    {
        var nonce = new byte[AesGcm.NonceByteSizes.MaxSize]; // 12 bytes for GCM
        var tag = new byte[AesGcm.TagByteSizes.MaxSize]; // 16 bytes
        var ciphertext = new byte[plaintext.Length];

        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(nonce);
        }

        using (var aesGcm = new AesGcm(key, AesGcm.TagByteSizes.MaxSize))
        {
            aesGcm.Encrypt(nonce, plaintext, ciphertext, tag);
        }

        return (ciphertext, nonce, tag);
    }

    private static byte[] DecryptAesGcm(byte[] ciphertext, byte[] key, byte[] nonce, byte[] tag)
    {
        var plaintext = new byte[ciphertext.Length];

        using (var aesGcm = new AesGcm(key, tag.Length))
        {
            aesGcm.Decrypt(nonce, ciphertext, tag, plaintext);
        }

        return plaintext;
    }
}
