using Microsoft.Extensions.Logging;
using Moq;
using USP.Infrastructure.Services.PAM.Connectors;
using Xunit;

namespace USP.IntegrationTests.PAM;

public class MongoDbConnectorTests : IClassFixture<TestDatabaseFixture>
{
    private readonly ILogger<MongoDbConnector> _logger;
    private readonly MongoDbConnector _connector;

    public MongoDbConnectorTests()
    {
        _logger = Mock.Of<ILogger<MongoDbConnector>>();
        _connector = new MongoDbConnector(_logger);
    }

    [Fact]
    public void Platform_ShouldReturnMongoDB()
    {
        // Act
        var platform = _connector.Platform;

        // Assert
        Assert.Equal("MongoDB", platform);
    }

    [Fact]
    public void GeneratePassword_ShouldReturnValidPassword()
    {
        // Act
        var password = _connector.GeneratePassword();

        // Assert
        Assert.NotNull(password);
        Assert.Equal(32, password.Length);
        Assert.Contains(password, c => char.IsUpper(c));
        Assert.Contains(password, c => char.IsLower(c));
        Assert.Contains(password, c => char.IsDigit(c));
    }

    [Fact]
    public async Task RotatePasswordAsync_WithInvalidHost_ShouldFail()
    {
        // Arrange
        var hostAddress = "invalid-host-12345.local";
        var username = "testuser";
        var currentPassword = "currentpass";
        var newPassword = _connector.GeneratePassword();

        // Act
        var result = await _connector.RotatePasswordAsync(
            hostAddress,
            27017,
            username,
            currentPassword,
            newPassword);

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task VerifyCredentialsAsync_WithInvalidHost_ShouldReturnFalse()
    {
        // Arrange
        var hostAddress = "invalid-host-12345.local";
        var username = "testuser";
        var password = "testpass";

        // Act
        var result = await _connector.VerifyCredentialsAsync(
            hostAddress,
            27017,
            username,
            password);

        // Assert
        Assert.False(result);
    }

    [Fact(Skip = "Requires MongoDB server - integration test")]
    public async Task RotatePasswordAsync_WithValidCredentials_ShouldSucceed()
    {
        // This test requires a real MongoDB server
        // To run: docker run -d -p 27017:27017 -e MONGO_INITDB_ROOT_USERNAME=admin -e MONGO_INITDB_ROOT_PASSWORD=password mongo:latest

        // Arrange
        var hostAddress = "localhost";
        var username = "admin";
        var currentPassword = "password";
        var newPassword = _connector.GeneratePassword();

        // Act
        var result = await _connector.RotatePasswordAsync(
            hostAddress,
            27017,
            username,
            currentPassword,
            newPassword);

        // Assert
        Assert.True(result.Success, result.ErrorMessage);
        Assert.NotNull(result.Details);

        // Verify new password works
        var verified = await _connector.VerifyCredentialsAsync(
            hostAddress,
            27017,
            username,
            newPassword);

        Assert.True(verified);
    }
}
