using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using USP.Core.Services.Cryptography;
using USP.Infrastructure.Data;
using USP.Infrastructure.Services.Secrets;
using USP.Infrastructure.Services.Secrets.DatabaseConnectors;
using Xunit;

namespace USP.IntegrationTests.Secrets;

/// <summary>
/// Tests for DatabaseEngine static credential rotation
/// Validates that NotSupportedException is thrown with clear error message
/// </summary>
public class DatabaseEngineStaticRotationTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly IEncryptionService _encryptionService;
    private readonly ILogger<DatabaseEngine> _logger;
    private readonly DatabaseEngine _engine;

    public DatabaseEngineStaticRotationTests()
    {
        // Setup in-memory database for testing
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;

        _context = new ApplicationDbContext(options);
        _encryptionService = Mock.Of<IEncryptionService>();
        _logger = Mock.Of<ILogger<DatabaseEngine>>();

        // Create mock connectors
        var mockConnectors = new List<IDatabaseConnector>
        {
            new PostgreSqlConnector(Mock.Of<ILogger<PostgreSqlConnector>>()),
            new MySqlConnector(Mock.Of<ILogger<MySqlConnector>>())
        };

        _engine = new DatabaseEngine(_context, _encryptionService, _logger, mockConnectors);
    }

    [Fact]
    public async Task RotateStaticCredentialsAsync_ShouldThrowNotSupportedException()
    {
        // Arrange
        var databaseName = "test-db";
        var roleName = "test-role";
        var userId = Guid.NewGuid();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotSupportedException>(
            () => _engine.RotateStaticCredentialsAsync(databaseName, roleName, userId));

        Assert.NotNull(exception);
        Assert.Contains("not currently supported", exception.Message);
    }

    [Fact]
    public async Task RotateStaticCredentialsAsync_ShouldProvideHelpfulErrorMessage()
    {
        // Arrange
        var databaseName = "production-db";
        var roleName = "app-user";
        var userId = Guid.NewGuid();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotSupportedException>(
            () => _engine.RotateStaticCredentialsAsync(databaseName, roleName, userId));

        Assert.Contains("Static credential rotation", exception.Message);
        Assert.Contains("Use dynamic credentials", exception.Message);
        Assert.Contains("GenerateCredentialsAsync", exception.Message);
        Assert.Contains("future release", exception.Message);
    }

    [Fact]
    public async Task RotateStaticCredentialsAsync_ShouldNotThrowNotImplementedException()
    {
        // Arrange
        var databaseName = "any-db";
        var roleName = "any-role";
        var userId = Guid.NewGuid();

        // Act & Assert
        try
        {
            await _engine.RotateStaticCredentialsAsync(databaseName, roleName, userId);
            Assert.True(false, "Expected NotSupportedException but no exception was thrown");
        }
        catch (NotSupportedException)
        {
            // Expected - this is the correct exception type
            Assert.True(true);
        }
        catch (NotImplementedException)
        {
            Assert.True(false, "NotImplementedException should not be thrown - use NotSupportedException instead");
        }
    }

    [Fact]
    public async Task RotateStaticCredentialsAsync_ErrorMessage_ShouldMentionDynamicCredentialsAlternative()
    {
        // Arrange
        var databaseName = "test-database";
        var roleName = "readonly-role";
        var userId = Guid.NewGuid();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotSupportedException>(
            () => _engine.RotateStaticCredentialsAsync(databaseName, roleName, userId));

        // Verify error message provides actionable guidance
        Assert.Contains("dynamic credentials", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("automatic expiration", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("TTL", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        _context?.Dispose();
    }
}
