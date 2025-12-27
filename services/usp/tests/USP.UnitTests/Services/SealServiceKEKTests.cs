using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;
using USP.Infrastructure.Persistence;
using USP.Infrastructure.Services.Secrets;

namespace USP.UnitTests.Services;

/// <summary>
/// Tests for SealService with KEK-based master key encryption.
/// Verifies that master key is encrypted with KEK instead of itself.
/// </summary>
public class SealServiceKEKTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly SealService _sealService;
    private readonly ILogger<SealService> _logger;
    private readonly ITestOutputHelper _output;
    private readonly string _originalKek;

    public SealServiceKEKTests(ITestOutputHelper output)
    {
        _output = output;

        // Generate a test KEK (32 bytes, Base64 encoded)
        var kekBytes = new byte[32];
        using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
        {
            rng.GetBytes(kekBytes);
        }
        var testKek = Convert.ToBase64String(kekBytes);

        // Store original KEK and set test KEK
        _originalKek = Environment.GetEnvironmentVariable("USP_KEY_ENCRYPTION_KEY");
        Environment.SetEnvironmentVariable("USP_KEY_ENCRYPTION_KEY", testKek);

        _output.WriteLine($"Test KEK set: {testKek[..16]}...");

        // Create in-memory database
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestSealDB_{Guid.NewGuid()}")
            .Options;

        _context = new ApplicationDbContext(options);

        // Create logger
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddProvider(new XunitLoggerProvider(output));
        });
        _logger = loggerFactory.CreateLogger<SealService>();

        _sealService = new SealService(_context, _logger);
    }

    [Fact]
    public async Task InitializeAsync_WithValidKEK_ShouldSucceed()
    {
        // Act
        var result = await _sealService.InitializeAsync(secretShares: 5, secretThreshold: 3);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(5, result.SecretShares);
        Assert.Equal(3, result.SecretThreshold);
        Assert.Equal(5, result.UnsealKeys.Count);
        Assert.NotEmpty(result.RootToken);

        _output.WriteLine($"✓ Vault initialized with {result.SecretShares} shares, threshold {result.SecretThreshold}");
    }

    [Fact]
    public async Task InitializeAsync_WithoutKEK_ShouldThrowException()
    {
        // Arrange
        Environment.SetEnvironmentVariable("USP_KEY_ENCRYPTION_KEY", null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sealService.InitializeAsync(secretShares: 5, secretThreshold: 3)
        );

        Assert.Contains("USP_KEY_ENCRYPTION_KEY", exception.Message);
        _output.WriteLine($"✓ Correctly throws exception when KEK is missing");
    }

    [Fact]
    public async Task InitializeAsync_WithInvalidKEKLength_ShouldThrowException()
    {
        // Arrange - Set KEK that's not 32 bytes
        Environment.SetEnvironmentVariable("USP_KEY_ENCRYPTION_KEY", Convert.ToBase64String(new byte[16]));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sealService.InitializeAsync(secretShares: 5, secretThreshold: 3)
        );

        Assert.Contains("32 bytes", exception.Message);
        _output.WriteLine($"✓ Correctly validates KEK length");
    }

    [Fact]
    public async Task SealUnsealWorkflow_WithKEK_ShouldWorkCorrectly()
    {
        // Step 1: Initialize
        var initResult = await _sealService.InitializeAsync(secretShares: 5, secretThreshold: 3);
        _output.WriteLine("Step 1: Vault initialized");

        // Verify the encrypted master key was saved
        var configAfterInit = await _context.SealConfigurations.FindAsync("default");
        Assert.NotNull(configAfterInit);
        Assert.NotNull(configAfterInit.EncryptedMasterKey);
        Assert.NotEmpty(configAfterInit.EncryptedMasterKey);
        _output.WriteLine($"✓ Encrypted master key saved ({configAfterInit.EncryptedMasterKey.Length} bytes)");

        // Verify unsealed after init
        var statusAfterInit = await _sealService.GetSealStatusAsync();
        Assert.False(statusAfterInit.Sealed);
        Assert.True(statusAfterInit.Initialized);
        _output.WriteLine("✓ Vault is unsealed after initialization");

        // Step 2: Seal
        await _sealService.SealAsync();
        var statusAfterSeal = await _sealService.GetSealStatusAsync();
        Assert.True(statusAfterSeal.Sealed);
        _output.WriteLine("Step 2: Vault sealed");

        // Verify master key is cleared
        var masterKeyAfterSeal = _sealService.GetMasterKey();
        Assert.Null(masterKeyAfterSeal);
        _output.WriteLine("✓ Master key cleared from memory");

        // Verify encrypted master key is still in database
        var configAfterSeal = await _context.SealConfigurations.FindAsync("default");
        Assert.NotNull(configAfterSeal);
        Assert.NotNull(configAfterSeal.EncryptedMasterKey);
        _output.WriteLine("✓ Encrypted master key still in database");

        // Step 3: Unseal with threshold keys
        _output.WriteLine("Step 3: Unsealing with threshold keys...");

        // Detach the entity to force fresh read from database
        _context.Entry(configAfterSeal).State = Microsoft.EntityFrameworkCore.EntityState.Detached;

        var status1 = await _sealService.UnsealAsync(initResult.UnsealKeys[0]);
        Assert.True(status1.Sealed);
        Assert.Equal(1, status1.Progress);
        _output.WriteLine($"  Key 1: Progress {status1.Progress}/{status1.Threshold}");

        var status2 = await _sealService.UnsealAsync(initResult.UnsealKeys[1]);
        Assert.True(status2.Sealed);
        Assert.Equal(2, status2.Progress);
        _output.WriteLine($"  Key 2: Progress {status2.Progress}/{status2.Threshold}");

        var status3 = await _sealService.UnsealAsync(initResult.UnsealKeys[2]);
        Assert.False(status3.Sealed); // Should be unsealed now
        Assert.Equal(0, status3.Progress); // Progress resets when unsealed
        _output.WriteLine($"  Key 3: Vault unsealed!");

        // Verify master key is available
        var masterKeyAfterUnseal = _sealService.GetMasterKey();
        Assert.NotNull(masterKeyAfterUnseal);
        Assert.Equal(32, masterKeyAfterUnseal.Length);
        _output.WriteLine($"✓ Master key recovered ({masterKeyAfterUnseal.Length} bytes)");
    }

    [Fact]
    public async Task UnsealAsync_WithSameKeyTwice_ShouldThrowException()
    {
        // Arrange
        var initResult = await _sealService.InitializeAsync(secretShares: 5, secretThreshold: 3);
        await _sealService.SealAsync();

        // Act - Submit same key twice
        await _sealService.UnsealAsync(initResult.UnsealKeys[0]);

        // Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sealService.UnsealAsync(initResult.UnsealKeys[0])
        );

        Assert.Contains("already been provided", exception.Message);
        _output.WriteLine("✓ Correctly prevents duplicate key submission");
    }

    [Fact]
    public async Task MasterKeyEncryption_UsesKEK_NotSelf()
    {
        // This test verifies that the master key is NOT encrypted with itself
        // by checking that changing the KEK breaks decryption

        // Arrange
        var initResult = await _sealService.InitializeAsync(secretShares: 5, secretThreshold: 3);
        await _sealService.SealAsync();

        // Change KEK to a different value
        var newKekBytes = new byte[32];
        using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
        {
            rng.GetBytes(newKekBytes);
        }
        Environment.SetEnvironmentVariable("USP_KEY_ENCRYPTION_KEY", Convert.ToBase64String(newKekBytes));
        _output.WriteLine("KEK changed to different value");

        // Act & Assert - Unseal should fail with wrong KEK
        await _sealService.UnsealAsync(initResult.UnsealKeys[0]);
        await _sealService.UnsealAsync(initResult.UnsealKeys[1]);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sealService.UnsealAsync(initResult.UnsealKeys[2])
        );

        Assert.Contains("verification failed", exception.Message);
        _output.WriteLine("✓ Verification fails with wrong KEK, proving master key is encrypted with KEK");
    }

    public void Dispose()
    {
        // Restore original KEK
        Environment.SetEnvironmentVariable("USP_KEY_ENCRYPTION_KEY", _originalKek);
        _context?.Dispose();
    }
}

/// <summary>
/// Xunit logger provider for test output
/// </summary>
internal class XunitLoggerProvider : ILoggerProvider
{
    private readonly ITestOutputHelper _output;

    public XunitLoggerProvider(ITestOutputHelper output)
    {
        _output = output;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new XunitLogger(_output, categoryName);
    }

    public void Dispose() { }
}

internal class XunitLogger : ILogger
{
    private readonly ITestOutputHelper _output;
    private readonly string _categoryName;

    public XunitLogger(ITestOutputHelper output, string categoryName)
    {
        _output = output;
        _categoryName = categoryName;
    }

    public IDisposable BeginScope<TState>(TState state) => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
    {
        _output.WriteLine($"[{logLevel}] {_categoryName}: {formatter(state, exception)}");
        if (exception != null)
        {
            _output.WriteLine($"  Exception: {exception}");
        }
    }
}
