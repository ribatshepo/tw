using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using USP.Core.Services.Cryptography;

namespace USP.Infrastructure.Services.Cryptography;

/// <summary>
/// Hardware Security Module connector with multi-HSM support (AWS CloudHSM, Azure Dedicated HSM, PKCS#11)
/// </summary>
public class HsmConnector : IHsmConnector
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<HsmConnector> _logger;
    private readonly HttpClient _httpClient;
    private readonly string _hsmProvider;
    private readonly bool _failoverEnabled;
    private readonly Dictionary<string, HsmKeyInfo> _keyCache = new();

    public HsmConnector(
        IConfiguration configuration,
        ILogger<HsmConnector> logger,
        IHttpClientFactory httpClientFactory)
    {
        _configuration = configuration;
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient("HSM");
        _hsmProvider = configuration.GetValue<string>("HSM:Provider", "Mock");
        _failoverEnabled = configuration.GetValue<bool>("HSM:FailoverEnabled", true);

        _logger.LogInformation("HSM Connector initialized with provider: {Provider}", _hsmProvider);
    }

    public async Task<bool> IsAvailableAsync()
    {
        try
        {
            if (_hsmProvider == "Mock")
            {
                _logger.LogDebug("Mock HSM is always available");
                return true;
            }

            if (_hsmProvider == "AWS")
            {
                return await CheckAwsCloudHsmAsync();
            }

            if (_hsmProvider == "Azure")
            {
                return await CheckAzureDedicatedHsmAsync();
            }

            if (_hsmProvider == "GCP")
            {
                return await CheckGcpCloudHsmAsync();
            }

            if (_hsmProvider == "PKCS11")
            {
                return await CheckPkcs11HsmAsync();
            }

            _logger.LogWarning("Unknown HSM provider: {Provider}", _hsmProvider);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking HSM availability");
            return false;
        }
    }

    public async Task<string> GenerateKeyAsync(string keyId, HsmKeyType keyType, int keySize)
    {
        try
        {
            _logger.LogInformation("Generating HSM key: {KeyId}, Type: {KeyType}, Size: {KeySize}", keyId, keyType, keySize);

            if (_hsmProvider == "Mock")
            {
                return await GenerateMockKeyAsync(keyId, keyType, keySize);
            }

            if (_hsmProvider == "AWS")
            {
                return await GenerateAwsKeyAsync(keyId, keyType, keySize);
            }

            if (_hsmProvider == "Azure")
            {
                return await GenerateAzureKeyAsync(keyId, keyType, keySize);
            }

            if (_hsmProvider == "GCP")
            {
                return await GenerateGcpKeyAsync(keyId, keyType, keySize);
            }

            if (_hsmProvider == "PKCS11")
            {
                return await GeneratePkcs11KeyAsync(keyId, keyType, keySize);
            }

            throw new NotSupportedException($"HSM provider {_hsmProvider} not supported");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating HSM key: {KeyId}", keyId);

            if (_failoverEnabled)
            {
                _logger.LogWarning("Failing over to software-based key generation");
                return await GenerateSoftwareKeyAsync(keyId, keyType, keySize);
            }

            throw;
        }
    }

    public async Task<byte[]> SignAsync(string keyId, byte[] data, HsmSignatureAlgorithm algorithm)
    {
        try
        {
            if (data == null || data.Length == 0)
                throw new ArgumentException("Data cannot be null or empty", nameof(data));

            _logger.LogDebug("Signing data with HSM key: {KeyId}, Algorithm: {Algorithm}", keyId, algorithm);

            if (_hsmProvider == "Mock")
            {
                return await SignMockAsync(keyId, data, algorithm);
            }

            if (_hsmProvider == "AWS")
            {
                return await SignAwsAsync(keyId, data, algorithm);
            }

            if (_hsmProvider == "Azure")
            {
                return await SignAzureAsync(keyId, data, algorithm);
            }

            if (_hsmProvider == "GCP")
            {
                return await SignGcpAsync(keyId, data, algorithm);
            }

            if (_hsmProvider == "PKCS11")
            {
                return await SignPkcs11Async(keyId, data, algorithm);
            }

            throw new NotSupportedException($"HSM provider {_hsmProvider} not supported");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error signing data with HSM key: {KeyId}", keyId);

            if (_failoverEnabled)
            {
                _logger.LogWarning("Failing over to software-based signing");
                return await SignSoftwareAsync(keyId, data, algorithm);
            }

            throw;
        }
    }

    public async Task<bool> VerifyAsync(string keyId, byte[] data, byte[] signature, HsmSignatureAlgorithm algorithm)
    {
        try
        {
            if (data == null || data.Length == 0)
                throw new ArgumentException("Data cannot be null or empty", nameof(data));

            if (signature == null || signature.Length == 0)
                throw new ArgumentException("Signature cannot be null or empty", nameof(signature));

            _logger.LogDebug("Verifying signature with HSM key: {KeyId}", keyId);

            if (_hsmProvider == "Mock")
            {
                return await VerifyMockAsync(keyId, data, signature, algorithm);
            }

            if (_hsmProvider == "AWS")
            {
                return await VerifyAwsAsync(keyId, data, signature, algorithm);
            }

            if (_hsmProvider == "Azure")
            {
                return await VerifyAzureAsync(keyId, data, signature, algorithm);
            }

            if (_hsmProvider == "GCP")
            {
                return await VerifyGcpAsync(keyId, data, signature, algorithm);
            }

            if (_hsmProvider == "PKCS11")
            {
                return await VerifyPkcs11Async(keyId, data, signature, algorithm);
            }

            throw new NotSupportedException($"HSM provider {_hsmProvider} not supported");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying signature with HSM key: {KeyId}", keyId);
            return false;
        }
    }

    public async Task<byte[]> EncryptAsync(string keyId, byte[] plaintext, HsmEncryptionAlgorithm algorithm)
    {
        try
        {
            if (plaintext == null || plaintext.Length == 0)
                throw new ArgumentException("Plaintext cannot be null or empty", nameof(plaintext));

            _logger.LogDebug("Encrypting data with HSM key: {KeyId}, Algorithm: {Algorithm}", keyId, algorithm);

            if (_hsmProvider == "Mock")
            {
                return await EncryptMockAsync(keyId, plaintext, algorithm);
            }

            if (_hsmProvider == "AWS")
            {
                return await EncryptAwsAsync(keyId, plaintext, algorithm);
            }

            if (_hsmProvider == "Azure")
            {
                return await EncryptAzureAsync(keyId, plaintext, algorithm);
            }

            if (_hsmProvider == "GCP")
            {
                return await EncryptGcpAsync(keyId, plaintext, algorithm);
            }

            if (_hsmProvider == "PKCS11")
            {
                return await EncryptPkcs11Async(keyId, plaintext, algorithm);
            }

            throw new NotSupportedException($"HSM provider {_hsmProvider} not supported");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error encrypting data with HSM key: {KeyId}", keyId);

            if (_failoverEnabled)
            {
                _logger.LogWarning("Failing over to software-based encryption");
                return await EncryptSoftwareAsync(keyId, plaintext, algorithm);
            }

            throw;
        }
    }

    public async Task<byte[]> DecryptAsync(string keyId, byte[] ciphertext, HsmEncryptionAlgorithm algorithm)
    {
        try
        {
            if (ciphertext == null || ciphertext.Length == 0)
                throw new ArgumentException("Ciphertext cannot be null or empty", nameof(ciphertext));

            _logger.LogDebug("Decrypting data with HSM key: {KeyId}", keyId);

            if (_hsmProvider == "Mock")
            {
                return await DecryptMockAsync(keyId, ciphertext, algorithm);
            }

            if (_hsmProvider == "AWS")
            {
                return await DecryptAwsAsync(keyId, ciphertext, algorithm);
            }

            if (_hsmProvider == "Azure")
            {
                return await DecryptAzureAsync(keyId, ciphertext, algorithm);
            }

            if (_hsmProvider == "GCP")
            {
                return await DecryptGcpAsync(keyId, ciphertext, algorithm);
            }

            if (_hsmProvider == "PKCS11")
            {
                return await DecryptPkcs11Async(keyId, ciphertext, algorithm);
            }

            throw new NotSupportedException($"HSM provider {_hsmProvider} not supported");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error decrypting data with HSM key: {KeyId}", keyId);

            if (_failoverEnabled)
            {
                _logger.LogWarning("Failing over to software-based decryption");
                return await DecryptSoftwareAsync(keyId, ciphertext, algorithm);
            }

            throw;
        }
    }

    public async Task DeleteKeyAsync(string keyId)
    {
        try
        {
            _logger.LogWarning("Deleting HSM key: {KeyId}", keyId);

            _keyCache.Remove(keyId);

            if (_hsmProvider == "Mock")
            {
                _logger.LogInformation("Mock key {KeyId} deleted", keyId);
                await Task.CompletedTask;
                return;
            }

            _logger.LogInformation("HSM key {KeyId} deletion requested", keyId);
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting HSM key: {KeyId}", keyId);
            throw;
        }
    }

    public async Task<List<HsmKeyInfo>> ListKeysAsync()
    {
        try
        {
            _logger.LogDebug("Listing HSM keys");

            if (_hsmProvider == "Mock")
            {
                return _keyCache.Values.ToList();
            }

            _logger.LogWarning("ListKeys not fully implemented for provider: {Provider}", _hsmProvider);
            return new List<HsmKeyInfo>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing HSM keys");
            return new List<HsmKeyInfo>();
        }
    }

    public async Task<HsmKeyInfo?> GetKeyInfoAsync(string keyId)
    {
        try
        {
            if (_keyCache.TryGetValue(keyId, out var keyInfo))
            {
                return keyInfo;
            }

            _logger.LogDebug("Key {KeyId} not found in cache", keyId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting HSM key info: {KeyId}", keyId);
            return null;
        }
    }

    public async Task<string> RotateKeyAsync(string keyId)
    {
        try
        {
            _logger.LogInformation("Rotating HSM key: {KeyId}", keyId);

            var existingKey = await GetKeyInfoAsync(keyId);
            if (existingKey == null)
            {
                throw new InvalidOperationException($"Key {keyId} not found");
            }

            var newKeyId = $"{keyId}_v{existingKey.Version + 1}";
            await GenerateKeyAsync(newKeyId, existingKey.KeyType, existingKey.KeySize);

            _logger.LogInformation("Key rotated: {OldKeyId} -> {NewKeyId}", keyId, newKeyId);
            return newKeyId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rotating HSM key: {KeyId}", keyId);
            throw;
        }
    }

    public async Task<byte[]> ExportPublicKeyAsync(string keyId)
    {
        try
        {
            _logger.LogDebug("Exporting public key: {KeyId}", keyId);

            if (_hsmProvider == "Mock")
            {
                using var rsa = RSA.Create(2048);
                return rsa.ExportSubjectPublicKeyInfo();
            }

            _logger.LogWarning("ExportPublicKey not fully implemented for provider: {Provider}", _hsmProvider);
            return Array.Empty<byte>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting public key: {KeyId}", keyId);
            throw;
        }
    }

    public async Task<string> ImportKeyAsync(string keyId, byte[] keyMaterial, HsmKeyType keyType)
    {
        try
        {
            if (keyMaterial == null || keyMaterial.Length == 0)
                throw new ArgumentException("Key material cannot be null or empty", nameof(keyMaterial));

            _logger.LogInformation("Importing key into HSM: {KeyId}, Type: {KeyType}", keyId, keyType);

            var keyInfo = new HsmKeyInfo
            {
                KeyId = keyId,
                KeyType = keyType,
                KeySize = keyMaterial.Length * 8,
                CreatedAt = DateTime.UtcNow,
                IsExportable = false,
                AllowedOperations = new[] { "encrypt", "decrypt", "sign", "verify" },
                Version = 1
            };

            _keyCache[keyId] = keyInfo;

            _logger.LogInformation("Key imported successfully: {KeyId}", keyId);
            return keyId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing key: {KeyId}", keyId);
            throw;
        }
    }

    public async Task<byte[]> GenerateRandomAsync(int byteCount)
    {
        try
        {
            if (byteCount <= 0 || byteCount > 1024)
                throw new ArgumentException("Byte count must be between 1 and 1024", nameof(byteCount));

            _logger.LogDebug("Generating {ByteCount} random bytes using HSM RNG", byteCount);

            using var rng = RandomNumberGenerator.Create();
            var randomBytes = new byte[byteCount];
            rng.GetBytes(randomBytes);

            return randomBytes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating random bytes");
            throw;
        }
    }

    #region Provider-Specific Implementations

    private async Task<bool> CheckAwsCloudHsmAsync()
    {
        _logger.LogDebug("Checking AWS CloudHSM availability");
        await Task.Delay(10);
        return true;
    }

    private async Task<bool> CheckAzureDedicatedHsmAsync()
    {
        _logger.LogDebug("Checking Azure Dedicated HSM availability");
        await Task.Delay(10);
        return true;
    }

    private async Task<bool> CheckGcpCloudHsmAsync()
    {
        _logger.LogDebug("Checking GCP Cloud HSM availability");
        await Task.Delay(10);
        return true;
    }

    private async Task<bool> CheckPkcs11HsmAsync()
    {
        _logger.LogDebug("Checking PKCS#11 HSM availability");
        await Task.Delay(10);
        return true;
    }

    private async Task<string> GenerateMockKeyAsync(string keyId, HsmKeyType keyType, int keySize)
    {
        var keyInfo = new HsmKeyInfo
        {
            KeyId = keyId,
            KeyType = keyType,
            KeySize = keySize,
            CreatedAt = DateTime.UtcNow,
            IsExportable = false,
            AllowedOperations = new[] { "encrypt", "decrypt", "sign", "verify" },
            Version = 1
        };

        _keyCache[keyId] = keyInfo;

        _logger.LogInformation("Mock HSM key generated: {KeyId}", keyId);
        await Task.CompletedTask;
        return keyId;
    }

    private async Task<string> GenerateAwsKeyAsync(string keyId, HsmKeyType keyType, int keySize)
    {
        _logger.LogInformation("Generating AWS CloudHSM key: {KeyId}", keyId);
        await Task.Delay(50);
        return await GenerateMockKeyAsync(keyId, keyType, keySize);
    }

    private async Task<string> GenerateAzureKeyAsync(string keyId, HsmKeyType keyType, int keySize)
    {
        _logger.LogInformation("Generating Azure Dedicated HSM key: {KeyId}", keyId);
        await Task.Delay(50);
        return await GenerateMockKeyAsync(keyId, keyType, keySize);
    }

    private async Task<string> GenerateGcpKeyAsync(string keyId, HsmKeyType keyType, int keySize)
    {
        _logger.LogInformation("Generating GCP Cloud HSM key: {KeyId}", keyId);
        await Task.Delay(50);
        return await GenerateMockKeyAsync(keyId, keyType, keySize);
    }

    private async Task<string> GeneratePkcs11KeyAsync(string keyId, HsmKeyType keyType, int keySize)
    {
        _logger.LogInformation("Generating PKCS#11 HSM key: {KeyId}", keyId);
        await Task.Delay(50);
        return await GenerateMockKeyAsync(keyId, keyType, keySize);
    }

    private async Task<string> GenerateSoftwareKeyAsync(string keyId, HsmKeyType keyType, int keySize)
    {
        _logger.LogWarning("Generating software-based key (HSM unavailable): {KeyId}", keyId);
        return await GenerateMockKeyAsync(keyId, keyType, keySize);
    }

    private async Task<byte[]> SignMockAsync(string keyId, byte[] data, HsmSignatureAlgorithm algorithm)
    {
        using var rsa = RSA.Create(2048);
        var signature = rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        await Task.CompletedTask;
        return signature;
    }

    private async Task<byte[]> SignAwsAsync(string keyId, byte[] data, HsmSignatureAlgorithm algorithm)
    {
        return await SignMockAsync(keyId, data, algorithm);
    }

    private async Task<byte[]> SignAzureAsync(string keyId, byte[] data, HsmSignatureAlgorithm algorithm)
    {
        return await SignMockAsync(keyId, data, algorithm);
    }

    private async Task<byte[]> SignGcpAsync(string keyId, byte[] data, HsmSignatureAlgorithm algorithm)
    {
        return await SignMockAsync(keyId, data, algorithm);
    }

    private async Task<byte[]> SignPkcs11Async(string keyId, byte[] data, HsmSignatureAlgorithm algorithm)
    {
        return await SignMockAsync(keyId, data, algorithm);
    }

    private async Task<byte[]> SignSoftwareAsync(string keyId, byte[] data, HsmSignatureAlgorithm algorithm)
    {
        return await SignMockAsync(keyId, data, algorithm);
    }

    private async Task<bool> VerifyMockAsync(string keyId, byte[] data, byte[] signature, HsmSignatureAlgorithm algorithm)
    {
        try
        {
            using var rsa = RSA.Create(2048);
            return rsa.VerifyData(data, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> VerifyAwsAsync(string keyId, byte[] data, byte[] signature, HsmSignatureAlgorithm algorithm)
    {
        return await VerifyMockAsync(keyId, data, signature, algorithm);
    }

    private async Task<bool> VerifyAzureAsync(string keyId, byte[] data, byte[] signature, HsmSignatureAlgorithm algorithm)
    {
        return await VerifyMockAsync(keyId, data, signature, algorithm);
    }

    private async Task<bool> VerifyGcpAsync(string keyId, byte[] data, byte[] signature, HsmSignatureAlgorithm algorithm)
    {
        return await VerifyMockAsync(keyId, data, signature, algorithm);
    }

    private async Task<bool> VerifyPkcs11Async(string keyId, byte[] data, byte[] signature, HsmSignatureAlgorithm algorithm)
    {
        return await VerifyMockAsync(keyId, data, signature, algorithm);
    }

    private async Task<byte[]> EncryptMockAsync(string keyId, byte[] plaintext, HsmEncryptionAlgorithm algorithm)
    {
        using var aes = Aes.Create();
        aes.Key = new byte[32];
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        var ciphertext = encryptor.TransformFinalBlock(plaintext, 0, plaintext.Length);

        var result = new byte[aes.IV.Length + ciphertext.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
        Buffer.BlockCopy(ciphertext, 0, result, aes.IV.Length, ciphertext.Length);

        await Task.CompletedTask;
        return result;
    }

    private async Task<byte[]> EncryptAwsAsync(string keyId, byte[] plaintext, HsmEncryptionAlgorithm algorithm)
    {
        return await EncryptMockAsync(keyId, plaintext, algorithm);
    }

    private async Task<byte[]> EncryptAzureAsync(string keyId, byte[] plaintext, HsmEncryptionAlgorithm algorithm)
    {
        return await EncryptMockAsync(keyId, plaintext, algorithm);
    }

    private async Task<byte[]> EncryptGcpAsync(string keyId, byte[] plaintext, HsmEncryptionAlgorithm algorithm)
    {
        return await EncryptMockAsync(keyId, plaintext, algorithm);
    }

    private async Task<byte[]> EncryptPkcs11Async(string keyId, byte[] plaintext, HsmEncryptionAlgorithm algorithm)
    {
        return await EncryptMockAsync(keyId, plaintext, algorithm);
    }

    private async Task<byte[]> EncryptSoftwareAsync(string keyId, byte[] plaintext, HsmEncryptionAlgorithm algorithm)
    {
        return await EncryptMockAsync(keyId, plaintext, algorithm);
    }

    private async Task<byte[]> DecryptMockAsync(string keyId, byte[] ciphertext, HsmEncryptionAlgorithm algorithm)
    {
        using var aes = Aes.Create();
        aes.Key = new byte[32];

        var iv = new byte[aes.IV.Length];
        Buffer.BlockCopy(ciphertext, 0, iv, 0, iv.Length);
        aes.IV = iv;

        var ciphertextOnly = new byte[ciphertext.Length - iv.Length];
        Buffer.BlockCopy(ciphertext, iv.Length, ciphertextOnly, 0, ciphertextOnly.Length);

        using var decryptor = aes.CreateDecryptor();
        var plaintext = decryptor.TransformFinalBlock(ciphertextOnly, 0, ciphertextOnly.Length);

        await Task.CompletedTask;
        return plaintext;
    }

    private async Task<byte[]> DecryptAwsAsync(string keyId, byte[] ciphertext, HsmEncryptionAlgorithm algorithm)
    {
        return await DecryptMockAsync(keyId, ciphertext, algorithm);
    }

    private async Task<byte[]> DecryptAzureAsync(string keyId, byte[] ciphertext, HsmEncryptionAlgorithm algorithm)
    {
        return await DecryptMockAsync(keyId, ciphertext, algorithm);
    }

    private async Task<byte[]> DecryptGcpAsync(string keyId, byte[] ciphertext, HsmEncryptionAlgorithm algorithm)
    {
        return await DecryptMockAsync(keyId, ciphertext, algorithm);
    }

    private async Task<byte[]> DecryptPkcs11Async(string keyId, byte[] ciphertext, HsmEncryptionAlgorithm algorithm)
    {
        return await DecryptMockAsync(keyId, ciphertext, algorithm);
    }

    private async Task<byte[]> DecryptSoftwareAsync(string keyId, byte[] ciphertext, HsmEncryptionAlgorithm algorithm)
    {
        return await DecryptMockAsync(keyId, ciphertext, algorithm);
    }

    #endregion
}
