using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using USP.Core.Models.DTOs.Seal;
using USP.Core.Models.Entities;
using USP.Core.Services.Cryptography;
using USP.Infrastructure.Data;

namespace USP.Infrastructure.Services.Cryptography;

/// <summary>
/// Manages seal/unseal operations using Shamir's Secret Sharing
/// Protects the master encryption key by splitting it into multiple shares
/// </summary>
public class SealManager : ISealManager
{
    private readonly ApplicationDbContext _context;
    private readonly IShamirSecretSharing _shamirService;
    private readonly ILogger<SealManager> _logger;

    // In-memory state
    private byte[]? _masterKey;
    private readonly List<byte[]> _unsealProgress = new();
    private readonly object _lockObject = new();

    public SealManager(
        ApplicationDbContext context,
        IShamirSecretSharing shamirService,
        ILogger<SealManager> logger)
    {
        _context = context;
        _shamirService = shamirService;
        _logger = logger;
    }

    public async Task<InitializeSealResponse> InitializeAsync(InitializeSealRequest request)
    {
        if (request.SecretThreshold < 2)
        {
            throw new ArgumentException("Threshold must be at least 2", nameof(request));
        }

        if (request.SecretShares < request.SecretThreshold)
        {
            throw new ArgumentException("Secret shares must be >= threshold", nameof(request));
        }

        if (request.SecretShares > 255)
        {
            throw new ArgumentException("Secret shares cannot exceed 255", nameof(request));
        }

        var existing = await _context.SealConfigurations.FirstOrDefaultAsync();
        if (existing != null && existing.Initialized)
        {
            throw new InvalidOperationException("Seal is already initialized");
        }

        _logger.LogInformation("Initializing seal with {Shares} shares and threshold {Threshold}",
            request.SecretShares, request.SecretThreshold);

        // Generate master key (32 bytes for AES-256)
        var masterKey = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(masterKey);
        }

        // Split master key using Shamir's Secret Sharing
        var shares = _shamirService.Split(masterKey, request.SecretThreshold, request.SecretShares);

        // Encrypt the master key with itself for storage validation
        var encryptedMasterKey = EncryptMasterKey(masterKey, masterKey);

        // Store seal configuration
        var config = new SealConfiguration
        {
            Id = Guid.NewGuid(),
            Version = 1,
            SecretThreshold = request.SecretThreshold,
            SecretShares = request.SecretShares,
            EncryptedMasterKey = Convert.ToBase64String(encryptedMasterKey),
            Initialized = true,
            CreatedAt = DateTime.UtcNow
        };

        if (existing != null)
        {
            _context.SealConfigurations.Remove(existing);
        }

        _context.SealConfigurations.Add(config);
        await _context.SaveChangesAsync();

        // Generate root token
        var rootToken = GenerateRootToken();

        _logger.LogWarning("Seal initialized successfully. Key shares MUST be distributed securely!");

        return new InitializeSealResponse
        {
            Keys = shares.Select(s => Convert.ToBase64String(s)).ToList(),
            RootToken = rootToken,
            KeysBase64 = request.SecretShares,
            KeysRequired = request.SecretThreshold
        };
    }

    public async Task<SealStatusResponse> UnsealAsync(UnsealRequest request)
    {
        var config = await _context.SealConfigurations.FirstOrDefaultAsync();
        if (config == null || !config.Initialized)
        {
            throw new InvalidOperationException("Seal has not been initialized");
        }

        lock (_lockObject)
        {
            // Reset unseal progress if requested
            if (request.Reset)
            {
                _unsealProgress.Clear();
                _logger.LogInformation("Unseal progress reset");
                return GetStatusResponseLocked(config, false);
            }

            // If already unsealed, return status
            if (_masterKey != null)
            {
                _logger.LogInformation("System is already unsealed");
                return GetStatusResponseLocked(config, true);
            }

            // Decode and add the share
            byte[] share;
            try
            {
                share = Convert.FromBase64String(request.Key);
            }
            catch (FormatException)
            {
                throw new ArgumentException("Invalid key format");
            }

            // Check for duplicate shares
            foreach (var existingShare in _unsealProgress)
            {
                if (existingShare.Length > 0 && share.Length > 0 && existingShare[0] == share[0])
                {
                    _logger.LogWarning("Duplicate share submitted (index {Index})", share[0]);
                    throw new InvalidOperationException("Duplicate share");
                }
            }

            _unsealProgress.Add(share);
            _logger.LogInformation("Unseal progress: {Progress}/{Threshold}", _unsealProgress.Count, config.SecretThreshold);

            // Check if we have enough shares
            if (_unsealProgress.Count >= config.SecretThreshold)
            {
                try
                {
                    // Reconstruct master key
                    var reconstructedKey = _shamirService.Combine(_unsealProgress.ToArray());

                    // Validate by decrypting the stored encrypted master key
                    var storedEncrypted = Convert.FromBase64String(config.EncryptedMasterKey);
                    var decrypted = DecryptMasterKey(storedEncrypted, reconstructedKey);

                    // Verify the decrypted key matches the reconstructed key
                    if (!decrypted.SequenceEqual(reconstructedKey))
                    {
                        _logger.LogError("Master key validation failed - key mismatch");
                        _unsealProgress.Clear();
                        throw new InvalidOperationException("Invalid unseal keys");
                    }

                    // Store master key in memory
                    _masterKey = reconstructedKey;
                    _unsealProgress.Clear();

                    // Update last unsealed timestamp
                    config.LastUnsealedAt = DateTime.UtcNow;
                    _context.SealConfigurations.Update(config);
                    _context.SaveChangesAsync().Wait();

                    _logger.LogWarning("System unsealed successfully");

                    return GetStatusResponseLocked(config, true);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during unseal");
                    _unsealProgress.Clear();
                    throw;
                }
            }

            return GetStatusResponseLocked(config, false);
        }
    }

    public async Task<SealResponse> SealAsync()
    {
        lock (_lockObject)
        {
            if (_masterKey != null)
            {
                // Clear master key from memory
                Array.Clear(_masterKey, 0, _masterKey.Length);
                _masterKey = null;
            }

            _unsealProgress.Clear();

            _logger.LogWarning("System sealed");
        }

        return new SealResponse { Sealed = true };
    }

    public async Task<SealStatusResponse> GetStatusAsync()
    {
        var config = await _context.SealConfigurations.FirstOrDefaultAsync();

        lock (_lockObject)
        {
            if (config == null)
            {
                return new SealStatusResponse
                {
                    Sealed = true,
                    Initialized = false,
                    Threshold = 0,
                    TotalShares = 0,
                    Progress = 0
                };
            }

            return GetStatusResponseLocked(config, _masterKey != null);
        }
    }

    public bool IsSealed()
    {
        lock (_lockObject)
        {
            return _masterKey == null;
        }
    }

    public byte[]? GetMasterKey()
    {
        lock (_lockObject)
        {
            return _masterKey;
        }
    }

    #region Private Helper Methods

    private SealStatusResponse GetStatusResponseLocked(SealConfiguration config, bool unsealed)
    {
        return new SealStatusResponse
        {
            Sealed = !unsealed,
            Threshold = config.SecretThreshold,
            TotalShares = config.SecretShares,
            Progress = unsealed ? 0 : _unsealProgress.Count,
            Version = config.Version,
            Initialized = config.Initialized
        };
    }

    private byte[] EncryptMasterKey(byte[] masterKey, byte[] encryptionKey)
    {
        using var aes = Aes.Create();
        aes.Key = encryptionKey;
        aes.Mode = CipherMode.CBC;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        var encrypted = encryptor.TransformFinalBlock(masterKey, 0, masterKey.Length);

        // Prepend IV to encrypted data
        var result = new byte[aes.IV.Length + encrypted.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
        Buffer.BlockCopy(encrypted, 0, result, aes.IV.Length, encrypted.Length);

        return result;
    }

    private byte[] DecryptMasterKey(byte[] encryptedData, byte[] decryptionKey)
    {
        using var aes = Aes.Create();
        aes.Key = decryptionKey;
        aes.Mode = CipherMode.CBC;

        // Extract IV
        var iv = new byte[aes.IV.Length];
        Buffer.BlockCopy(encryptedData, 0, iv, 0, iv.Length);
        aes.IV = iv;

        // Extract encrypted data
        var encrypted = new byte[encryptedData.Length - iv.Length];
        Buffer.BlockCopy(encryptedData, iv.Length, encrypted, 0, encrypted.Length);

        using var decryptor = aes.CreateDecryptor();
        return decryptor.TransformFinalBlock(encrypted, 0, encrypted.Length);
    }

    private string GenerateRootToken()
    {
        var tokenBytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(tokenBytes);
        }
        return Convert.ToBase64String(tokenBytes);
    }

    #endregion
}
