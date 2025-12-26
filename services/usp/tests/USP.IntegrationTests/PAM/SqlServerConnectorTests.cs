using Microsoft.Extensions.Logging;
using Moq;
using USP.Infrastructure.Services.PAM.Connectors;
using Xunit;

namespace USP.IntegrationTests.PAM;

public class SqlServerConnectorTests : IClassFixture<TestDatabaseFixture>
{
    private readonly ILogger<SqlServerConnector> _logger;
    private readonly SqlServerConnector _connector;

    public SqlServerConnectorTests()
    {
        _logger = Mock.Of<ILogger<SqlServerConnector>>();
        _connector = new SqlServerConnector(_logger);
    }

    [Fact]
    public void Platform_ShouldReturnSQLServer()
    {
        // Act
        var platform = _connector.Platform;

        // Assert
        Assert.Equal("SQLServer", platform);
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
            1433,
            username,
            currentPassword,
            newPassword,
            "master");

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task VerifyCredentialsAsync_WithInvalidHost_ShouldReturnFalse()
    {
        // Arrange
        var hostAddress = "invalid-host-12345.local";
        var username = "sa";
        var password = "testpass";

        // Act
        var result = await _connector.VerifyCredentialsAsync(
            hostAddress,
            1433,
            username,
            password,
            "master");

        // Assert
        Assert.False(result);
    }

    [Fact(Skip = "Requires SQL Server - integration test")]
    public async Task RotatePasswordAsync_WithValidCredentials_ShouldSucceed()
    {
        // This test requires a real SQL Server instance
        // To run: docker run -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=MyPass@word123" -p 1433:1433 -d mcr.microsoft.com/mssql/server:2022-latest

        // Arrange
        var hostAddress = "localhost";
        var username = "sa";
        var currentPassword = "MyPass@word123";
        var newPassword = _connector.GeneratePassword() + "!1Aa"; // Ensure complexity

        // Act
        var result = await _connector.RotatePasswordAsync(
            hostAddress,
            1433,
            username,
            currentPassword,
            newPassword,
            "master");

        // Assert
        Assert.True(result.Success, result.ErrorMessage);
        Assert.NotNull(result.Details);

        // Verify new password works
        var verified = await _connector.VerifyCredentialsAsync(
            hostAddress,
            1433,
            username,
            newPassword,
            "master");

        Assert.True(verified);
    }

    [Fact(Skip = "Requires SQL Server - integration test")]
    public async Task DiscoverPrivilegedLoginsAsync_ShouldReturnLogins()
    {
        // Arrange
        var hostAddress = "localhost";
        var username = "sa";
        var password = "MyPass@word123";

        // Act
        var logins = await _connector.DiscoverPrivilegedLoginsAsync(
            hostAddress,
            1433,
            username,
            password,
            "master");

        // Assert
        Assert.NotNull(logins);
        Assert.Contains("sa", logins);
    }
}
