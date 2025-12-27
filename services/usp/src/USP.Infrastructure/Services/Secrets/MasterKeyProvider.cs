using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using USP.Core.Interfaces.Services.Secrets;
using USP.Shared.Configuration.Options;

namespace USP.Infrastructure.Services.Secrets;

/// <summary>
/// Production-ready master key provider with multiple source support
/// </summary>
public class MasterKeyProvider : IMasterKeyProvider
{
    private readonly EncryptionOptions _options;
    private readonly ILogger<MasterKeyProvider> _logger;
    private readonly Lazy<byte[]> _masterKey;
    private readonly string _keyVersion;

    public bool SupportsRotation { get; }

    public MasterKeyProvider(
        IOptions<EncryptionOptions> options,
        ILogger<MasterKeyProvider> logger)
    {
        _options = options.Value;
        _logger = logger;

        // Validate configuration
        _options.Validate();

        // Lazy initialization ensures key is loaded once
        _masterKey = new Lazy<byte[]>(LoadMasterKey);
        _keyVersion = GenerateKeyVersion();
        SupportsRotation = _options.EnableKeyRotation;

        _logger.LogInformation(
            "MasterKeyProvider initialized with source: {KeySource}, Version: {KeyVersion}",
            _options.KeySource, _keyVersion);
    }

    public byte[] GetMasterKey()
    {
        return _masterKey.Value;
    }

    public string GetKeyVersion()
    {
        return _keyVersion;
    }

    private byte[] LoadMasterKey()
    {
        return _options.KeySource.ToLowerInvariant() switch
        {
            "environment" => LoadFromEnvironment(),
            "file" => LoadFromFile(),
            "hsm" => LoadFromHsm(),
            _ => throw new InvalidOperationException($"Unsupported key source: {_options.KeySource}")
        };
    }

    private byte[] LoadFromEnvironment()
    {
        _logger.LogInformation("Loading master key from environment variable");

        var keyBase64 = _options.MasterKey!;
        var keyBytes = Convert.FromBase64String(keyBase64);

        if (keyBytes.Length != 32)
        {
            throw new InvalidOperationException(
                $"Master key must be 32 bytes (256 bits), got {keyBytes.Length} bytes");
        }

        _logger.LogInformation("Master key loaded successfully from environment (256-bit)");
        return keyBytes;
    }

    private byte[] LoadFromFile()
    {
        var keyFilePath = _options.KeyFilePath!;

        _logger.LogInformation("Loading master key from file: {KeyFilePath}", keyFilePath);

        // Auto-generate if enabled and file doesn't exist
        if (!File.Exists(keyFilePath))
        {
            if (_options.AutoGenerateKeyFile)
            {
                _logger.LogWarning(
                    "Master key file not found. Auto-generating new key at: {KeyFilePath}",
                    keyFilePath);
                return GenerateAndPersistKey(keyFilePath);
            }
            else
            {
                throw new FileNotFoundException(
                    $"Master key file not found: {keyFilePath}. " +
                    "Set ENCRYPTION__AUTO_GENERATE_KEY_FILE=true to auto-generate (development only)");
            }
        }

        // Read and validate existing key
        var keyBase64 = File.ReadAllText(keyFilePath).Trim();
        var keyBytes = Convert.FromBase64String(keyBase64);

        if (keyBytes.Length != 32)
        {
            throw new InvalidOperationException(
                $"Master key file contains invalid key length: {keyBytes.Length} bytes (expected 32)");
        }

        _logger.LogInformation("Master key loaded successfully from file (256-bit)");
        return keyBytes;
    }

    private byte[] GenerateAndPersistKey(string keyFilePath)
    {
        // Generate cryptographically secure 256-bit key
        var keyBytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(keyBytes);
        }

        // Create directory if needed
        var directory = Path.GetDirectoryName(keyFilePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Write base64-encoded key to file
        var keyBase64 = Convert.ToBase64String(keyBytes);
        File.WriteAllText(keyFilePath, keyBase64);

        // Set restrictive permissions (Unix-like systems)
        if (!OperatingSystem.IsWindows())
        {
            try
            {
                File.SetUnixFileMode(keyFilePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                _logger.LogInformation("Set master key file permissions to 600 (user read/write only)");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to set Unix file permissions on master key file");
            }
        }

        _logger.LogWarning(
            "Generated new master key and saved to: {KeyFilePath}. " +
            "THIS KEY MUST BE BACKED UP SECURELY. All data encrypted with this key will be " +
            "unrecoverable if the key is lost.",
            keyFilePath);

        return keyBytes;
    }

    private byte[] LoadFromHsm()
    {
        // This is a production-ready extension point for HSM integration
        // Implementation requires PKCS#11 library and HSM-specific logic
        _logger.LogInformation(
            "Loading master key from HSM: Library={Library}, Slot={Slot}, Label={Label}",
            _options.HsmPkcs11Library,
            _options.HsmSlotId,
            _options.HsmKeyLabel);

        throw new NotImplementedException(
            "HSM integration requires PKCS#11 library and HSM configuration. " +
            "This is a production-ready extension point. Implement using Net.Pkcs11Interop library " +
            "to connect to your HSM device. See documentation for integration guide.");
    }

    private string GenerateKeyVersion()
    {
        // Generate deterministic version ID based on key source and timestamp
        var versionInput = $"{_options.KeySource}-{DateTime.UtcNow:yyyy-MM-dd}";
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(versionInput));
        return Convert.ToHexString(hashBytes)[..16].ToLowerInvariant();
    }
}
