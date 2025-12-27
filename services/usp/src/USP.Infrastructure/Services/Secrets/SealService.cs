using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using USP.Core.Domain.Entities.Vault;
using USP.Core.Interfaces.Services.Secrets;
using USP.Infrastructure.Cryptography;
using USP.Infrastructure.Metrics;
using USP.Infrastructure.Persistence;

namespace USP.Infrastructure.Services.Secrets;

/// <summary>
/// Implements seal/unseal operations for the vault using Shamir's Secret Sharing.
/// </summary>
public class SealService : ISealService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<SealService> _logger;

    // In-memory state (lost on restart - requires unseal after restart)
    private static byte[]? _masterKey = null;
    private static readonly List<byte[]> _unsealKeysSubmitted = new();
    private static readonly object _lock = new();

    public SealService(
        ApplicationDbContext context,
        ILogger<SealService> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<InitializeResult> InitializeAsync(
        int secretShares = 5,
        int secretThreshold = 3,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Initializing vault with {Shares} shares and {Threshold} threshold",
            secretShares, secretThreshold);

        // Check if already initialized
        var existingConfig = await _context.SealConfigurations
            .FirstOrDefaultAsync(c => c.Id == "default", cancellationToken);

        if (existingConfig?.Initialized == true)
        {
            _logger.LogWarning("Vault is already initialized");
            throw new InvalidOperationException("Vault is already initialized");
        }

        // Validate parameters
        if (secretShares < 2 || secretShares > 255)
            throw new ArgumentException("Secret shares must be between 2 and 255", nameof(secretShares));

        if (secretThreshold < 2 || secretThreshold > secretShares)
            throw new ArgumentException("Threshold must be between 2 and total shares", nameof(secretThreshold));

        // Generate a cryptographically secure master key (32 bytes = 256 bits)
        var masterKey = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(masterKey);
        }

        // Split the master key using Shamir's Secret Sharing
        var shares = ShamirSecretSharing.Split(masterKey, secretShares, secretThreshold);

        // Convert shares to Base64 and Hex for distribution
        var unsealKeysBase64 = shares.Select(s => Convert.ToBase64String(s)).ToList();
        var unsealKeysHex = shares.Select(s => Convert.ToHexString(s).ToLowerInvariant()).ToList();

        // Generate root token (cryptographically secure random token)
        var rootTokenBytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(rootTokenBytes);
        }
        var rootToken = Convert.ToBase64String(rootTokenBytes);

        // Encrypt the master key with itself for verification
        // (In production, this would be encrypted with a KEK)
        byte[] encryptedMasterKey;
        using (var aes = Aes.Create())
        {
            aes.Key = masterKey;
            aes.GenerateIV();

            using var encryptor = aes.CreateEncryptor();
            var plaintext = masterKey; // Encrypt the master key with itself
            var ciphertext = encryptor.TransformFinalBlock(plaintext, 0, plaintext.Length);

            // Prepend IV to ciphertext
            encryptedMasterKey = new byte[aes.IV.Length + ciphertext.Length];
            Buffer.BlockCopy(aes.IV, 0, encryptedMasterKey, 0, aes.IV.Length);
            Buffer.BlockCopy(ciphertext, 0, encryptedMasterKey, aes.IV.Length, ciphertext.Length);
        }

        // Create or update seal configuration
        var config = existingConfig ?? new SealConfiguration { Id = "default" };
        config.Initialized = true;
        config.SecretShares = secretShares;
        config.SecretThreshold = secretThreshold;
        config.EncryptedMasterKey = encryptedMasterKey;
        config.InitializedAt = DateTime.UtcNow;
        config.UpdatedAt = DateTime.UtcNow;

        if (existingConfig == null)
        {
            _context.SealConfigurations.Add(config);
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Vault initialized successfully");

        // Store master key in memory (vault is now unsealed)
        lock (_lock)
        {
            _masterKey = masterKey;
            _unsealKeysSubmitted.Clear();
        }

        // Record metrics
        SecurityMetrics.RecordVaultInitialization();
        SecurityMetrics.UpdateSealStatus(isSealed: false);

        return new InitializeResult
        {
            UnsealKeys = unsealKeysBase64,
            UnsealKeysHex = unsealKeysHex,
            RootToken = rootToken,
            SecretShares = secretShares,
            SecretThreshold = secretThreshold
        };
    }

    public async Task<SealStatus> UnsealAsync(
        string unsealKey,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Processing unseal key");

        // Get seal configuration
        var config = await _context.SealConfigurations
            .FirstOrDefaultAsync(c => c.Id == "default", cancellationToken);

        if (config == null || !config.Initialized)
        {
            throw new InvalidOperationException("Vault is not initialized");
        }

        // Check if already unsealed
        if (!IsSealed())
        {
            _logger.LogInformation("Vault is already unsealed");
            return await GetSealStatusAsync(cancellationToken);
        }

        // Decode unseal key (try Base64 first, then Hex)
        byte[] unsealKeyBytes;
        try
        {
            unsealKeyBytes = Convert.FromBase64String(unsealKey);
        }
        catch
        {
            try
            {
                unsealKeyBytes = Convert.FromHexString(unsealKey);
            }
            catch
            {
                throw new ArgumentException("Invalid unseal key format (expected Base64 or Hex)");
            }
        }

        lock (_lock)
        {
            // Check if this key was already submitted
            if (_unsealKeysSubmitted.Any(k => k.SequenceEqual(unsealKeyBytes)))
            {
                _logger.LogWarning("Unseal key already submitted");
                throw new InvalidOperationException("This unseal key has already been provided");
            }

            // Add to submitted keys
            _unsealKeysSubmitted.Add(unsealKeyBytes);

            _logger.LogInformation("Unseal progress: {Progress}/{Threshold}",
                _unsealKeysSubmitted.Count, config.SecretThreshold);

            // Check if we have enough keys
            if (_unsealKeysSubmitted.Count >= config.SecretThreshold)
            {
                try
                {
                    // Reconstruct the master key
                    var reconstructedKey = ShamirSecretSharing.Combine(_unsealKeysSubmitted.ToArray());

                    // Verify the reconstructed key by decrypting the stored encrypted master key
                    bool isValid = VerifyMasterKey(reconstructedKey, config.EncryptedMasterKey!);

                    if (!isValid)
                    {
                        _logger.LogError("Failed to verify reconstructed master key");
                        _unsealKeysSubmitted.Clear();
                        throw new InvalidOperationException("Invalid unseal keys - master key verification failed");
                    }

                    // Store master key in memory
                    _masterKey = reconstructedKey;
                    _unsealKeysSubmitted.Clear();

                    // Update last unsealed timestamp
                    config.LastUnsealedAt = DateTime.UtcNow;
                    config.UpdatedAt = DateTime.UtcNow;
                    _context.SaveChangesAsync(cancellationToken).GetAwaiter().GetResult();

                    _logger.LogInformation("Vault unsealed successfully");

                    // Record metrics
                    SecurityMetrics.RecordUnsealOperation(success: true);
                    SecurityMetrics.UpdateSealStatus(isSealed: false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error reconstructing master key");
                    _unsealKeysSubmitted.Clear();

                    // Record failed unseal
                    SecurityMetrics.RecordUnsealOperation(success: false);

                    throw;
                }
            }
        }

        return await GetSealStatusAsync(cancellationToken);
    }

    public async Task<SealStatus> SealAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Sealing vault");

        var config = await _context.SealConfigurations
            .FirstOrDefaultAsync(c => c.Id == "default", cancellationToken);

        if (config == null || !config.Initialized)
        {
            throw new InvalidOperationException("Vault is not initialized");
        }

        lock (_lock)
        {
            // Clear master key from memory
            if (_masterKey != null)
            {
                Array.Clear(_masterKey, 0, _masterKey.Length);
                _masterKey = null;
            }

            // Clear any submitted unseal keys
            _unsealKeysSubmitted.Clear();
        }

        // Update last sealed timestamp
        config.LastSealedAt = DateTime.UtcNow;
        config.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Vault sealed successfully");

        // Record metrics
        SecurityMetrics.RecordSealOperation();
        SecurityMetrics.UpdateSealStatus(isSealed: true);

        return await GetSealStatusAsync(cancellationToken);
    }

    public async Task<SealStatus> GetSealStatusAsync(CancellationToken cancellationToken = default)
    {
        var config = await _context.SealConfigurations
            .FirstOrDefaultAsync(c => c.Id == "default", cancellationToken);

        bool initialized = config?.Initialized == true;
        bool sealed = IsSealed();
        int progress = 0;

        lock (_lock)
        {
            progress = _unsealKeysSubmitted.Count;
        }

        return new SealStatus
        {
            Sealed = sealed,
            Initialized = initialized,
            Threshold = config?.SecretThreshold ?? 0,
            SecretShares = config?.SecretShares ?? 0,
            Progress = sealed ? progress : 0,
            ClusterName = config?.ClusterName,
            ClusterId = config?.ClusterId,
            Version = config?.Version ?? "1.0.0"
        };
    }

    public bool IsSealed()
    {
        lock (_lock)
        {
            return _masterKey == null;
        }
    }

    public Task ResetUnsealProgressAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Resetting unseal progress");

        lock (_lock)
        {
            _unsealKeysSubmitted.Clear();
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that the reconstructed master key is correct by decrypting
    /// the encrypted master key stored during initialization.
    /// </summary>
    private bool VerifyMasterKey(byte[] masterKey, byte[] encryptedMasterKey)
    {
        try
        {
            using var aes = Aes.Create();
            aes.Key = masterKey;

            // Extract IV from encrypted data
            var iv = new byte[aes.IV.Length];
            Buffer.BlockCopy(encryptedMasterKey, 0, iv, 0, iv.Length);
            aes.IV = iv;

            // Extract ciphertext
            var ciphertext = new byte[encryptedMasterKey.Length - iv.Length];
            Buffer.BlockCopy(encryptedMasterKey, iv.Length, ciphertext, 0, ciphertext.Length);

            // Decrypt
            using var decryptor = aes.CreateDecryptor();
            var decrypted = decryptor.TransformFinalBlock(ciphertext, 0, ciphertext.Length);

            // Verify that decrypted value matches the master key
            return decrypted.SequenceEqual(masterKey);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets the current master key (only available when unsealed).
    /// </summary>
    public byte[]? GetMasterKey()
    {
        lock (_lock)
        {
            return _masterKey;
        }
    }
}
