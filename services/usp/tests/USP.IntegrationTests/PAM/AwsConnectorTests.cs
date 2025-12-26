using Microsoft.Extensions.Logging;
using Moq;
using USP.Infrastructure.Services.PAM.Connectors;
using Xunit;

namespace USP.IntegrationTests.PAM;

public class AwsConnectorTests : IClassFixture<TestDatabaseFixture>
{
    private readonly ILogger<AwsConnector> _logger;
    private readonly AwsConnector _connector;

    public AwsConnectorTests()
    {
        _logger = Mock.Of<ILogger<AwsConnector>>();
        _connector = new AwsConnector(_logger);
    }

    [Fact]
    public void Platform_ShouldReturnAWS()
    {
        // Act
        var platform = _connector.Platform;

        // Assert
        Assert.Equal("AWS", platform);
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
    public async Task RotatePasswordAsync_WithoutAccessKeyId_ShouldFail()
    {
        // Arrange
        var username = "testuser";
        var currentPassword = "currentSecretKey";
        var newPassword = _connector.GeneratePassword();
        var connectionDetails = "{\"region\":\"us-east-1\"}"; // Missing accessKeyId

        // Act
        var result = await _connector.RotatePasswordAsync(
            "iam.amazonaws.com",
            null,
            username,
            currentPassword,
            newPassword,
            null,
            connectionDetails);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("access key", result.ErrorMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RotatePasswordAsync_WithInvalidCredentials_ShouldFail()
    {
        // Arrange
        var username = "testuser";
        var currentAccessKey = "AKIAIOSFODNN7EXAMPLE";
        var currentSecretKey = "wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY";
        var connectionDetails = $"{{\"region\":\"us-east-1\",\"accessKeyId\":\"{currentAccessKey}\"}}";

        // Act
        var result = await _connector.RotatePasswordAsync(
            "iam.amazonaws.com",
            null,
            username,
            currentSecretKey,
            "newSecretKey",
            null,
            connectionDetails);

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task VerifyCredentialsAsync_WithInvalidCredentials_ShouldReturnFalse()
    {
        // Arrange
        var username = "testuser";
        var accessKeyId = "AKIAIOSFODNN7EXAMPLE";
        var secretKey = "wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY";
        var connectionDetails = $"{{\"region\":\"us-east-1\",\"accessKeyId\":\"{accessKeyId}\"}}";

        // Act
        var result = await _connector.VerifyCredentialsAsync(
            "iam.amazonaws.com",
            null,
            username,
            secretKey,
            null,
            connectionDetails);

        // Assert
        Assert.False(result);
    }

    [Fact(Skip = "Requires AWS account and credentials - integration test")]
    public async Task RotatePasswordAsync_WithValidCredentials_ShouldSucceed()
    {
        // This test requires real AWS credentials
        // Set environment variables: AWS_ACCESS_KEY_ID, AWS_SECRET_ACCESS_KEY, AWS_REGION

        // Arrange
        var username = "test-iam-user";
        var currentAccessKeyId = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID") ?? "invalid";
        var currentSecretKey = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY") ?? "invalid";
        var region = Environment.GetEnvironmentVariable("AWS_REGION") ?? "us-east-1";
        var connectionDetails = $"{{\"region\":\"{region}\",\"accessKeyId\":\"{currentAccessKeyId}\"}}";

        // Act
        var result = await _connector.RotatePasswordAsync(
            "iam.amazonaws.com",
            null,
            username,
            currentSecretKey,
            "newSecretKey",
            null,
            connectionDetails);

        // Assert
        Assert.True(result.Success, result.ErrorMessage);
        Assert.NotNull(result.Details);
    }

    [Fact(Skip = "Requires AWS account and credentials - integration test")]
    public async Task ListAccessKeysAsync_WithValidCredentials_ShouldReturnKeys()
    {
        // Arrange
        var username = "test-iam-user";
        var accessKeyId = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID") ?? "invalid";
        var secretKey = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY") ?? "invalid";
        var region = Environment.GetEnvironmentVariable("AWS_REGION") ?? "us-east-1";

        // Act
        var keys = await _connector.ListAccessKeysAsync(username, accessKeyId, secretKey, region);

        // Assert
        Assert.NotNull(keys);
        Assert.True(keys.Count >= 0);
    }

    [Fact(Skip = "Requires AWS account and credentials - integration test")]
    public async Task GetUserDetailsAsync_WithValidCredentials_ShouldReturnDetails()
    {
        // Arrange
        var username = "test-iam-user";
        var accessKeyId = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID") ?? "invalid";
        var secretKey = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY") ?? "invalid";
        var region = Environment.GetEnvironmentVariable("AWS_REGION") ?? "us-east-1";

        // Act
        var details = await _connector.GetUserDetailsAsync(username, accessKeyId, secretKey, region);

        // Assert
        Assert.NotNull(details);
        Assert.Equal(username, details.UserName);
        Assert.NotEmpty(details.UserId);
        Assert.NotEmpty(details.Arn);
    }
}
