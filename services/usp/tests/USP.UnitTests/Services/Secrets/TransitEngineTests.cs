using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using USP.Core.Models.DTOs.Transit;
using USP.Core.Models.Entities;
using USP.Core.Services.Cryptography;
using USP.Infrastructure.Data;
using USP.Infrastructure.Services.Secrets;
using USP.UnitTests.TestHelpers;

namespace USP.UnitTests.Services.Secrets;

public class TransitEngineTests : IDisposable
{
    private readonly TestApplicationDbContext _context;
    private readonly Mock<IEncryptionService> _encryptionServiceMock;
    private readonly Mock<ILogger<TransitEngine>> _loggerMock;
    private readonly TransitEngine _transitEngine;
    private readonly Guid _testUserId = Guid.NewGuid();

    public TransitEngineTests()
    {
        // Setup in-memory database with test-specific DbContext
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"TransitEngineTestDb_{Guid.NewGuid()}")
            .EnableSensitiveDataLogging()
            .Options;

        _context = new TestApplicationDbContext(options);
        // Note: Not calling EnsureCreated() - InMemory provider creates tables on-demand

        // Setup mocks
        _encryptionServiceMock = new Mock<IEncryptionService>();
        _loggerMock = new Mock<ILogger<TransitEngine>>();

        // Mock encryption service to return predictable values
        _encryptionServiceMock
            .Setup(x => x.Encrypt(It.IsAny<string>()))
            .Returns<string>(plaintext => $"encrypted_{plaintext}");

        _encryptionServiceMock
            .Setup(x => x.Decrypt(It.IsAny<string>()))
            .Returns<string>(ciphertext => ciphertext.Replace("encrypted_", ""));

        // Create service
        _transitEngine = new TransitEngine(_context, _encryptionServiceMock.Object, _loggerMock.Object);
    }

    public void Dispose()
    {
        // InMemory database is automatically cleaned up when context is disposed
        _context.Dispose();
    }

    #region Key Management Tests

    [Fact]
    public async Task CreateKeyAsync_WithValidRequest_CreatesKey()
    {
        // Arrange
        var keyName = "test-key";
        var request = new CreateKeyRequest
        {
            Type = "aes256-gcm96",
            DeletionAllowed = false,
            Exportable = false
        };

        // Act
        var response = await _transitEngine.CreateKeyAsync(keyName, request, _testUserId);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(keyName, response.Name);
        Assert.Equal("aes256-gcm96", response.Type);
        Assert.Equal(1, response.LatestVersion);

        // Verify database
        var key = await _context.TransitKeys.Include(tk => tk.Versions).FirstOrDefaultAsync(tk => tk.Name == keyName);
        Assert.NotNull(key);
        Assert.Single(key.Versions);
        Assert.Equal(1, key.Versions.First().Version);
    }

    [Fact]
    public async Task CreateKeyAsync_WithDuplicateName_ThrowsException()
    {
        // Arrange
        var keyName = "duplicate-key";
        var request = new CreateKeyRequest { Type = "aes256-gcm96" };

        await _transitEngine.CreateKeyAsync(keyName, request, _testUserId);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _transitEngine.CreateKeyAsync(keyName, request, _testUserId)
        );
    }

    [Fact]
    public async Task CreateKeyAsync_WithUnsupportedKeyType_ThrowsException()
    {
        // Arrange
        var request = new CreateKeyRequest { Type = "unsupported-type" };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _transitEngine.CreateKeyAsync("test-key", request, _testUserId)
        );
    }

    [Fact]
    public async Task ReadKeyAsync_WithExistingKey_ReturnsKeyMetadata()
    {
        // Arrange
        var keyName = "read-test-key";
        await _transitEngine.CreateKeyAsync(keyName, new CreateKeyRequest { Type = "aes256-gcm96" }, _testUserId);

        // Act
        var response = await _transitEngine.ReadKeyAsync(keyName, _testUserId);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(keyName, response.Name);
        Assert.Equal("aes256-gcm96", response.Type);
        Assert.Equal(1, response.LatestVersion);
        Assert.Single(response.Versions);
        Assert.Equal(0, response.EncryptionCount);
    }

    [Fact]
    public async Task ReadKeyAsync_WithNonExistentKey_ThrowsException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _transitEngine.ReadKeyAsync("non-existent-key", _testUserId)
        );
    }

    [Fact]
    public async Task ListKeysAsync_ReturnsAllKeys()
    {
        // Arrange
        await _transitEngine.CreateKeyAsync("key1", new CreateKeyRequest { Type = "aes256-gcm96" }, _testUserId);
        await _transitEngine.CreateKeyAsync("key2", new CreateKeyRequest { Type = "chacha20-poly1305" }, _testUserId);
        await _transitEngine.CreateKeyAsync("key3", new CreateKeyRequest { Type = "rsa-2048" }, _testUserId);

        // Act
        var response = await _transitEngine.ListKeysAsync(_testUserId);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(3, response.Keys.Count);
        Assert.Contains("key1", response.Keys);
        Assert.Contains("key2", response.Keys);
        Assert.Contains("key3", response.Keys);
    }

    [Fact]
    public async Task DeleteKeyAsync_WithDeletionAllowed_DeletesKey()
    {
        // Arrange
        var keyName = "delete-test-key";
        await _transitEngine.CreateKeyAsync(keyName, new CreateKeyRequest
        {
            Type = "aes256-gcm96",
            DeletionAllowed = true
        }, _testUserId);

        // Act
        await _transitEngine.DeleteKeyAsync(keyName, _testUserId);

        // Assert
        var key = await _context.TransitKeys.FirstOrDefaultAsync(tk => tk.Name == keyName);
        Assert.Null(key);
    }

    [Fact]
    public async Task DeleteKeyAsync_WithDeletionNotAllowed_ThrowsException()
    {
        // Arrange
        var keyName = "nodelete-key";
        await _transitEngine.CreateKeyAsync(keyName, new CreateKeyRequest
        {
            Type = "aes256-gcm96",
            DeletionAllowed = false
        }, _testUserId);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _transitEngine.DeleteKeyAsync(keyName, _testUserId)
        );
    }

    [Fact]
    public async Task UpdateKeyConfigAsync_UpdatesConfiguration()
    {
        // Arrange
        var keyName = "update-test-key";
        await _transitEngine.CreateKeyAsync(keyName, new CreateKeyRequest { Type = "aes256-gcm96" }, _testUserId);

        var updateRequest = new UpdateKeyConfigRequest
        {
            MinDecryptionVersion = 1,
            MinEncryptionVersion = 1,
            DeletionAllowed = true
        };

        // Act
        var response = await _transitEngine.UpdateKeyConfigAsync(keyName, updateRequest, _testUserId);

        // Assert
        Assert.Equal(1, response.MinDecryptionVersion);
        Assert.Equal(1, response.MinEncryptionVersion);
        Assert.True(response.DeletionAllowed);

        // Verify database
        var key = await _context.TransitKeys.FirstAsync(tk => tk.Name == keyName);
        Assert.True(key.DeletionAllowed);
    }

    [Fact]
    public async Task RotateKeyAsync_CreatesNewVersion()
    {
        // Arrange
        var keyName = "rotate-test-key";
        await _transitEngine.CreateKeyAsync(keyName, new CreateKeyRequest { Type = "aes256-gcm96" }, _testUserId);

        // Act
        var response = await _transitEngine.RotateKeyAsync(keyName, _testUserId);

        // Assert
        Assert.Equal(2, response.LatestVersion);

        // Verify database
        var key = await _context.TransitKeys.Include(tk => tk.Versions).FirstAsync(tk => tk.Name == keyName);
        Assert.Equal(2, key.LatestVersion);
        Assert.Equal(2, key.Versions.Count);
        Assert.Equal(2, key.MinEncryptionVersion); // Should auto-update to latest
    }

    #endregion

    #region Encryption Tests

    [Fact]
    public async Task EncryptAsync_WithAes256Gcm_EncryptsData()
    {
        // Arrange
        var keyName = "encrypt-test-key";
        await _transitEngine.CreateKeyAsync(keyName, new CreateKeyRequest { Type = "aes256-gcm96" }, _testUserId);

        var plaintext = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("Hello, World!"));
        var request = new EncryptRequest { Plaintext = plaintext };

        // Act
        var response = await _transitEngine.EncryptAsync(keyName, request, _testUserId);

        // Assert
        Assert.NotNull(response.Ciphertext);
        Assert.StartsWith("vault:v", response.Ciphertext);
        Assert.Equal(1, response.KeyVersion);

        // Verify usage count
        var key = await _context.TransitKeys.FirstAsync(tk => tk.Name == keyName);
        Assert.Equal(1, key.EncryptionCount);
    }

    [Fact]
    public async Task EncryptAsync_WithContext_UsesAEAD()
    {
        // Arrange
        var keyName = "context-encrypt-key";
        await _transitEngine.CreateKeyAsync(keyName, new CreateKeyRequest { Type = "aes256-gcm96" }, _testUserId);

        var plaintext = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("Sensitive data"));
        var context = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("user:123"));
        var request = new EncryptRequest
        {
            Plaintext = plaintext,
            Context = context
        };

        // Act
        var response = await _transitEngine.EncryptAsync(keyName, request, _testUserId);

        // Assert
        Assert.NotNull(response.Ciphertext);
        Assert.StartsWith("vault:v1:", response.Ciphertext);
    }

    [Fact]
    public async Task DecryptAsync_DecryptsValidCiphertext()
    {
        // Arrange
        var keyName = "decrypt-test-key";
        await _transitEngine.CreateKeyAsync(keyName, new CreateKeyRequest { Type = "aes256-gcm96" }, _testUserId);

        var plaintext = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("Secret message"));
        var encryptResponse = await _transitEngine.EncryptAsync(keyName, new EncryptRequest { Plaintext = plaintext }, _testUserId);

        // Act
        var decryptResponse = await _transitEngine.DecryptAsync(keyName, new DecryptRequest
        {
            Ciphertext = encryptResponse.Ciphertext
        }, _testUserId);

        // Assert
        Assert.Equal(plaintext, decryptResponse.Plaintext);
        Assert.Equal(1, decryptResponse.KeyVersion);

        // Verify usage count
        var key = await _context.TransitKeys.FirstAsync(tk => tk.Name == keyName);
        Assert.Equal(1, key.EncryptionCount);
        Assert.Equal(1, key.DecryptionCount);
    }

    [Fact]
    public async Task RewrapAsync_ReEncryptsWithLatestVersion()
    {
        // Arrange
        var keyName = "rewrap-test-key";
        await _transitEngine.CreateKeyAsync(keyName, new CreateKeyRequest { Type = "aes256-gcm96" }, _testUserId);

        var plaintext = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("Data to rewrap"));
        var encryptResponse = await _transitEngine.EncryptAsync(keyName, new EncryptRequest { Plaintext = plaintext }, _testUserId);

        // Rotate the key
        await _transitEngine.RotateKeyAsync(keyName, _testUserId);

        // Act
        var rewrapResponse = await _transitEngine.RewrapAsync(keyName, new RewrapRequest
        {
            Ciphertext = encryptResponse.Ciphertext
        }, _testUserId);

        // Assert
        Assert.NotEqual(encryptResponse.Ciphertext, rewrapResponse.Ciphertext);
        Assert.Equal(2, rewrapResponse.KeyVersion);
        Assert.StartsWith("vault:v2:", rewrapResponse.Ciphertext);

        // Verify decryption still works
        var decryptResponse = await _transitEngine.DecryptAsync(keyName, new DecryptRequest
        {
            Ciphertext = rewrapResponse.Ciphertext
        }, _testUserId);
        Assert.Equal(plaintext, decryptResponse.Plaintext);
    }

    #endregion

    #region Batch Operation Tests

    [Fact]
    public async Task BatchEncryptAsync_EncryptsMultipleItems()
    {
        // Arrange
        var keyName = "batch-encrypt-key";
        await _transitEngine.CreateKeyAsync(keyName, new CreateKeyRequest { Type = "aes256-gcm96" }, _testUserId);

        var request = new BatchEncryptRequest
        {
            BatchInput = new List<BatchEncryptItem>
            {
                new() { Plaintext = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("Message 1")) },
                new() { Plaintext = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("Message 2")) },
                new() { Plaintext = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("Message 3")) }
            }
        };

        // Act
        var response = await _transitEngine.BatchEncryptAsync(keyName, request, _testUserId);

        // Assert
        Assert.Equal(3, response.BatchResults.Count);
        Assert.All(response.BatchResults, result =>
        {
            Assert.NotNull(result.Ciphertext);
            Assert.StartsWith("vault:v1:", result.Ciphertext);
            Assert.Null(result.Error);
        });

        // Verify usage count
        var key = await _context.TransitKeys.FirstAsync(tk => tk.Name == keyName);
        Assert.Equal(3, key.EncryptionCount);
    }

    [Fact]
    public async Task BatchDecryptAsync_DecryptsMultipleItems()
    {
        // Arrange
        var keyName = "batch-decrypt-key";
        await _transitEngine.CreateKeyAsync(keyName, new CreateKeyRequest { Type = "aes256-gcm96" }, _testUserId);

        var plaintexts = new[]
        {
            Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("Message 1")),
            Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("Message 2")),
            Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("Message 3"))
        };

        var encryptResponse = await _transitEngine.BatchEncryptAsync(keyName, new BatchEncryptRequest
        {
            BatchInput = plaintexts.Select(p => new BatchEncryptItem { Plaintext = p }).ToList()
        }, _testUserId);

        // Act
        var decryptResponse = await _transitEngine.BatchDecryptAsync(keyName, new BatchDecryptRequest
        {
            BatchInput = encryptResponse.BatchResults
                .Select(r => new BatchDecryptItem { Ciphertext = r.Ciphertext })
                .ToList()
        }, _testUserId);

        // Assert
        Assert.Equal(3, decryptResponse.BatchResults.Count);
        for (int i = 0; i < 3; i++)
        {
            Assert.Equal(plaintexts[i], decryptResponse.BatchResults[i].Plaintext);
            Assert.Null(decryptResponse.BatchResults[i].Error);
        }
    }

    [Fact]
    public async Task BatchEncryptAsync_WithTooManyItems_ThrowsException()
    {
        // Arrange
        var keyName = "batch-limit-key";
        await _transitEngine.CreateKeyAsync(keyName, new CreateKeyRequest { Type = "aes256-gcm96" }, _testUserId);

        var request = new BatchEncryptRequest
        {
            BatchInput = Enumerable.Range(0, 1001)
                .Select(i => new BatchEncryptItem { Plaintext = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"Message {i}")) })
                .ToList()
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _transitEngine.BatchEncryptAsync(keyName, request, _testUserId)
        );
    }

    #endregion

    #region Data Key Generation Tests

    [Fact]
    public async Task GenerateDataKeyAsync_Returns256BitKey()
    {
        // Arrange
        var keyName = "dek-key";
        await _transitEngine.CreateKeyAsync(keyName, new CreateKeyRequest { Type = "aes256-gcm96" }, _testUserId);

        var request = new GenerateDataKeyRequest { Bits = 256 };

        // Act
        var response = await _transitEngine.GenerateDataKeyAsync(keyName, request, _testUserId);

        // Assert
        Assert.NotNull(response.Plaintext);
        Assert.NotNull(response.Ciphertext);
        Assert.StartsWith("vault:v1:", response.Ciphertext);

        // Verify key size (256 bits = 32 bytes = 44 characters in Base64)
        var dekBytes = Convert.FromBase64String(response.Plaintext);
        Assert.Equal(32, dekBytes.Length);
    }

    [Fact]
    public async Task GenerateDataKeyAsync_Returns512BitKey()
    {
        // Arrange
        var keyName = "dek-512-key";
        await _transitEngine.CreateKeyAsync(keyName, new CreateKeyRequest { Type = "aes256-gcm96" }, _testUserId);

        var request = new GenerateDataKeyRequest { Bits = 512 };

        // Act
        var response = await _transitEngine.GenerateDataKeyAsync(keyName, request, _testUserId);

        // Assert
        var dekBytes = Convert.FromBase64String(response.Plaintext);
        Assert.Equal(64, dekBytes.Length); // 512 bits = 64 bytes
    }

    [Fact]
    public async Task GenerateDataKeyAsync_WithInvalidBits_ThrowsException()
    {
        // Arrange
        var keyName = "dek-invalid-key";
        await _transitEngine.CreateKeyAsync(keyName, new CreateKeyRequest { Type = "aes256-gcm96" }, _testUserId);

        var request = new GenerateDataKeyRequest { Bits = 128 }; // Invalid

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _transitEngine.GenerateDataKeyAsync(keyName, request, _testUserId)
        );
    }

    #endregion

    #region Asymmetric Key Tests

    [Fact]
    public async Task SignAsync_WithRsaKey_GeneratesSignature()
    {
        // Arrange
        var keyName = "rsa-sign-key";
        await _transitEngine.CreateKeyAsync(keyName, new CreateKeyRequest { Type = "rsa-2048" }, _testUserId);

        var data = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("Data to sign"));
        var request = new SignRequest
        {
            Input = data,
            HashAlgorithm = "sha2-256"
        };

        // Act
        var response = await _transitEngine.SignAsync(keyName, request, _testUserId);

        // Assert
        Assert.NotNull(response.Signature);
        Assert.Equal(1, response.KeyVersion);

        // Verify usage count
        var key = await _context.TransitKeys.FirstAsync(tk => tk.Name == keyName);
        Assert.Equal(1, key.SigningCount);
    }

    [Fact]
    public async Task VerifyAsync_WithValidSignature_ReturnsTrue()
    {
        // Arrange
        var keyName = "rsa-verify-key";
        await _transitEngine.CreateKeyAsync(keyName, new CreateKeyRequest { Type = "rsa-2048" }, _testUserId);

        var data = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("Data to verify"));
        var signResponse = await _transitEngine.SignAsync(keyName, new SignRequest
        {
            Input = data,
            HashAlgorithm = "sha2-256"
        }, _testUserId);

        // Act
        var verifyResponse = await _transitEngine.VerifyAsync(keyName, new VerifyRequest
        {
            Input = data,
            Signature = signResponse.Signature,
            HashAlgorithm = "sha2-256"
        }, _testUserId);

        // Assert
        Assert.True(verifyResponse.Valid);

        // Verify usage count
        var key = await _context.TransitKeys.FirstAsync(tk => tk.Name == keyName);
        Assert.Equal(1, key.SigningCount);
        Assert.Equal(1, key.VerificationCount);
    }

    [Fact]
    public async Task SignAsync_WithSymmetricKey_ThrowsException()
    {
        // Arrange
        var keyName = "aes-sign-key";
        await _transitEngine.CreateKeyAsync(keyName, new CreateKeyRequest { Type = "aes256-gcm96" }, _testUserId);

        var request = new SignRequest
        {
            Input = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("Data")),
            HashAlgorithm = "sha2-256"
        };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _transitEngine.SignAsync(keyName, request, _testUserId)
        );
    }

    #endregion

    #region ChaCha20-Poly1305 Tests

    [Fact]
    public async Task EncryptAsync_WithChaCha20_EncryptsData()
    {
        // Arrange
        var keyName = "chacha-key";
        await _transitEngine.CreateKeyAsync(keyName, new CreateKeyRequest { Type = "chacha20-poly1305" }, _testUserId);

        var plaintext = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("ChaCha20 test"));
        var request = new EncryptRequest { Plaintext = plaintext };

        // Act
        var response = await _transitEngine.EncryptAsync(keyName, request, _testUserId);

        // Assert
        Assert.NotNull(response.Ciphertext);
        Assert.StartsWith("vault:v1:", response.Ciphertext);

        // Verify decryption works
        var decryptResponse = await _transitEngine.DecryptAsync(keyName, new DecryptRequest
        {
            Ciphertext = response.Ciphertext
        }, _testUserId);
        Assert.Equal(plaintext, decryptResponse.Plaintext);
    }

    #endregion
}
