using Microsoft.Extensions.Logging;
using Moq;
using USP.Infrastructure.Services.PAM.Connectors;
using Xunit;

namespace USP.IntegrationTests.PAM;

public class SshConnectorTests : IClassFixture<TestDatabaseFixture>
{
    private readonly ILogger<SshConnector> _logger;
    private readonly SshConnector _connector;

    public SshConnectorTests()
    {
        _logger = Mock.Of<ILogger<SshConnector>>();
        _connector = new SshConnector(_logger);
    }

    [Fact]
    public void Platform_ShouldReturnSSH()
    {
        // Act
        var platform = _connector.Platform;

        // Assert
        Assert.Equal("SSH", platform);
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
    public void GenerateSshKeyPair_ShouldReturnValidKeys()
    {
        // Act
        var (privateKey, publicKey) = _connector.GenerateSshKeyPair();

        // Assert
        Assert.NotNull(privateKey);
        Assert.NotNull(publicKey);
        Assert.Contains("-----BEGIN", privateKey);
        Assert.Contains("-----END", privateKey);
        Assert.StartsWith("ssh-rsa", publicKey);
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
            22,
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
            22,
            username,
            password);

        // Assert
        Assert.False(result);
    }

    [Fact(Skip = "Requires SSH server - integration test")]
    public async Task RotatePasswordAsync_WithValidCredentials_ShouldSucceed()
    {
        // This test requires a real SSH server
        // To run: docker run -d -p 2222:22 linuxserver/openssh-server

        // Arrange
        var hostAddress = "localhost";
        var port = 2222;
        var username = "testuser";
        var currentPassword = "testpass";
        var newPassword = _connector.GeneratePassword();

        // Act
        var result = await _connector.RotatePasswordAsync(
            hostAddress,
            port,
            username,
            currentPassword,
            newPassword);

        // Assert
        Assert.True(result.Success, result.ErrorMessage);
        Assert.NotNull(result.Details);

        // Verify new password works
        var verified = await _connector.VerifyCredentialsAsync(
            hostAddress,
            port,
            username,
            newPassword);

        Assert.True(verified);
    }
}
