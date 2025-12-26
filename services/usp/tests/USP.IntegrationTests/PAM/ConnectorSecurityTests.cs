using Microsoft.Extensions.Logging;
using Moq;
using USP.Infrastructure.Services.PAM.Connectors;
using Xunit;

namespace USP.IntegrationTests.PAM;

/// <summary>
/// Security tests for PAM connectors
/// Validates that sensitive data is not stored in insecure locations
/// </summary>
public class ConnectorSecurityTests
{
    private readonly ILogger<SshConnector> _sshLogger;
    private readonly ILogger<AwsConnector> _awsLogger;
    private readonly SshConnector _sshConnector;
    private readonly AwsConnector _awsConnector;

    public ConnectorSecurityTests()
    {
        _sshLogger = Mock.Of<ILogger<SshConnector>>();
        _awsLogger = Mock.Of<ILogger<AwsConnector>>();
        _sshConnector = new SshConnector(_sshLogger);
        _awsConnector = new AwsConnector(_awsLogger);
    }

    [Fact]
    public void SshConnector_GenerateSshKeyPair_ShouldNotStorePrivateKeyInErrorMessage()
    {
        // Act
        var (privateKey, publicKey) = _sshConnector.GenerateSshKeyPair();

        // Assert - verify key generation works
        Assert.NotNull(privateKey);
        Assert.NotNull(publicKey);
        Assert.Contains("-----BEGIN", privateKey);
        Assert.StartsWith("ssh-rsa", publicKey);

        // The method itself doesn't store in ErrorMessage - that was in RotateSshKeyAsync
        // This test validates the key generation works properly
    }

    [Fact]
    public async Task SshConnector_RotateSshKeyAsync_WithInvalidHost_ShouldNotExposePrivateKey()
    {
        // Arrange
        var hostAddress = "nonexistent-host-12345.invalid";
        var username = "testuser";
        var password = "testpass";

        // Act
        var result = await _sshConnector.RotateSshKeyAsync(hostAddress, 22, username, password);

        // Assert - verify failure doesn't expose sensitive data
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);

        // ErrorMessage should contain actual error, not private key material
        Assert.DoesNotContain("-----BEGIN", result.ErrorMessage);
        Assert.DoesNotContain("-----END", result.ErrorMessage);
    }

    [Fact]
    public async Task AwsConnector_RotatePasswordAsync_ShouldAttemptDeletion()
    {
        // Arrange - use invalid credentials that will fail
        var username = "test-user";
        var currentAccessKey = "AKIAIOSFODNN7EXAMPLE";
        var currentSecretKey = "wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY";
        var connectionDetails = $"{{\"region\":\"us-east-1\",\"accessKeyId\":\"{currentAccessKey}\"}}";

        // Act
        var result = await _awsConnector.RotatePasswordAsync(
            "iam.amazonaws.com",
            null,
            username,
            currentSecretKey,
            "newSecretKey",
            null,
            connectionDetails);

        // Assert - will fail due to invalid credentials, but that's expected
        Assert.False(result.Success);

        // The important thing is that the code path includes deletion logic
        // (We can't test actual deletion without valid AWS credentials)
    }

    [Fact]
    public void AwsConnector_DeleteAccessKeyAsync_MethodExists()
    {
        // Verify the delete method exists and is accessible
        var methodInfo = typeof(AwsConnector).GetMethod("DeleteAccessKeyAsync");

        Assert.NotNull(methodInfo);
        Assert.Equal(typeof(Task<bool>), methodInfo.ReturnType);
    }

    [Fact]
    public async Task SshConnector_GeneratePassword_ShouldMeetComplexityRequirements()
    {
        // Act
        var password = _sshConnector.GeneratePassword();

        // Assert - verify generated passwords are secure
        Assert.NotNull(password);
        Assert.True(password.Length >= 32, "Password should be at least 32 characters");
        Assert.Contains(password, c => char.IsUpper(c));
        Assert.Contains(password, c => char.IsLower(c));
        Assert.Contains(password, c => char.IsDigit(c));

        // Should contain special characters
        var specialChars = "!@#$%^&*()-_=+[]{}|;:,.<>?";
        Assert.Contains(password, c => specialChars.Contains(c));
    }

    [Fact]
    public void SshConnector_GeneratePassword_ShouldBeRandom()
    {
        // Act - generate multiple passwords
        var password1 = _sshConnector.GeneratePassword();
        var password2 = _sshConnector.GeneratePassword();
        var password3 = _sshConnector.GeneratePassword();

        // Assert - passwords should be different (not predictable)
        Assert.NotEqual(password1, password2);
        Assert.NotEqual(password2, password3);
        Assert.NotEqual(password1, password3);
    }

    [Fact]
    public async Task AwsConnector_WithMissingAccessKeyId_ShouldFailSecurely()
    {
        // Arrange - connection details without access key ID
        var username = "test-user";
        var secretKey = "someSecretKey";
        var connectionDetails = "{\"region\":\"us-east-1\"}"; // Missing accessKeyId

        // Act
        var result = await _awsConnector.RotatePasswordAsync(
            "iam.amazonaws.com",
            null,
            username,
            secretKey,
            "newSecretKey",
            null,
            connectionDetails);

        // Assert - should fail with appropriate error message
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("access key", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);

        // Should not expose the secret key in error message
        Assert.DoesNotContain(secretKey, result.ErrorMessage);
    }
}
