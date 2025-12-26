using Microsoft.Extensions.Logging;
using Moq;
using USP.Infrastructure.Services.PAM.Connectors;
using Xunit;

namespace USP.IntegrationTests.PAM;

public class RedisConnectorTests : IClassFixture<TestDatabaseFixture>
{
    private readonly ILogger<RedisConnector> _logger;
    private readonly RedisConnector _connector;

    public RedisConnectorTests()
    {
        _logger = Mock.Of<ILogger<RedisConnector>>();
        _connector = new RedisConnector(_logger);
    }

    [Fact]
    public void Platform_ShouldReturnRedis()
    {
        // Act
        var platform = _connector.Platform;

        // Assert
        Assert.Equal("Redis", platform);
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
        var username = "default";
        var currentPassword = "currentpass";
        var newPassword = _connector.GeneratePassword();

        // Act
        var result = await _connector.RotatePasswordAsync(
            hostAddress,
            6379,
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
        var username = "default";
        var password = "testpass";

        // Act
        var result = await _connector.VerifyCredentialsAsync(
            hostAddress,
            6379,
            username,
            password);

        // Assert
        Assert.False(result);
    }

    [Fact(Skip = "Requires Redis server - integration test")]
    public async Task RotatePasswordAsync_WithValidCredentials_ShouldSucceed()
    {
        // This test requires a real Redis server
        // To run: docker run -d -p 6379:6379 redis:latest redis-server --requirepass mypassword

        // Arrange
        var hostAddress = "localhost";
        var username = "default";
        var currentPassword = "mypassword";
        var newPassword = _connector.GeneratePassword();

        // Act
        var result = await _connector.RotatePasswordAsync(
            hostAddress,
            6379,
            username,
            currentPassword,
            newPassword);

        // Assert
        Assert.True(result.Success, result.ErrorMessage);
        Assert.NotNull(result.Details);

        // Verify new password works
        var verified = await _connector.VerifyCredentialsAsync(
            hostAddress,
            6379,
            username,
            newPassword);

        Assert.True(verified);
    }

    [Fact(Skip = "Requires Redis server - integration test")]
    public async Task ListAclUsersAsync_WithValidCredentials_ShouldReturnUsers()
    {
        // This test requires Redis 6+ with ACL support

        // Arrange
        var hostAddress = "localhost";
        var password = "mypassword";

        // Act
        var users = await _connector.ListAclUsersAsync(hostAddress, 6379, password);

        // Assert
        Assert.NotNull(users);
        Assert.Contains("default", users);
    }
}
