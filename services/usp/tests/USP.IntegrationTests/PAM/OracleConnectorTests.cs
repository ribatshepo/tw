using Microsoft.Extensions.Logging;
using Moq;
using USP.Infrastructure.Services.PAM.Connectors;
using Xunit;

namespace USP.IntegrationTests.PAM;

public class OracleConnectorTests : IClassFixture<TestDatabaseFixture>
{
    private readonly ILogger<OracleConnector> _logger;
    private readonly OracleConnector _connector;

    public OracleConnectorTests()
    {
        _logger = Mock.Of<ILogger<OracleConnector>>();
        _connector = new OracleConnector(_logger);
    }

    [Fact]
    public void Platform_ShouldReturnOracle()
    {
        // Act
        var platform = _connector.Platform;

        // Assert
        Assert.Equal("Oracle", platform);
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
            1521,
            username,
            currentPassword,
            newPassword,
            "ORCL");

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task VerifyCredentialsAsync_WithInvalidHost_ShouldReturnFalse()
    {
        // Arrange
        var hostAddress = "invalid-host-12345.local";
        var username = "system";
        var password = "testpass";

        // Act
        var result = await _connector.VerifyCredentialsAsync(
            hostAddress,
            1521,
            username,
            password,
            "ORCL");

        // Assert
        Assert.False(result);
    }

    [Fact(Skip = "Requires Oracle Database - integration test")]
    public async Task RotatePasswordAsync_WithValidCredentials_ShouldSucceed()
    {
        // This test requires a real Oracle Database instance
        // To run: docker run -d -p 1521:1521 -e ORACLE_PASSWORD=MyPass123 gvenzl/oracle-xe:latest

        // Arrange
        var hostAddress = "localhost";
        var username = "SYSTEM";
        var currentPassword = "MyPass123";
        var newPassword = _connector.GeneratePassword();

        // Act
        var result = await _connector.RotatePasswordAsync(
            hostAddress,
            1521,
            username,
            currentPassword,
            newPassword,
            "XEPDB1");

        // Assert
        Assert.True(result.Success, result.ErrorMessage);
        Assert.NotNull(result.Details);

        // Verify new password works
        var verified = await _connector.VerifyCredentialsAsync(
            hostAddress,
            1521,
            username,
            newPassword,
            "XEPDB1");

        Assert.True(verified);
    }

    [Fact(Skip = "Requires Oracle Database - integration test")]
    public async Task DiscoverPrivilegedUsersAsync_ShouldReturnUsers()
    {
        // Arrange
        var hostAddress = "localhost";
        var username = "SYSTEM";
        var password = "MyPass123";

        // Act
        var users = await _connector.DiscoverPrivilegedUsersAsync(
            hostAddress,
            1521,
            username,
            password,
            "XEPDB1");

        // Assert
        Assert.NotNull(users);
        Assert.Contains("SYSTEM", users);
    }

    [Fact(Skip = "Requires Oracle Database - integration test")]
    public async Task GetUserPrivilegesAsync_ShouldReturnPrivileges()
    {
        // Arrange
        var hostAddress = "localhost";
        var adminUsername = "SYSTEM";
        var adminPassword = "MyPass123";
        var targetUser = "SYSTEM";

        // Act
        var privileges = await _connector.GetUserPrivilegesAsync(
            hostAddress,
            1521,
            adminUsername,
            adminPassword,
            targetUser,
            "XEPDB1");

        // Assert
        Assert.NotNull(privileges);
        Assert.True(privileges.ContainsKey("Roles"));
        Assert.True(privileges.ContainsKey("SystemPrivileges"));
        Assert.True(privileges["Roles"].Count > 0 || privileges["SystemPrivileges"].Count > 0);
    }
}
