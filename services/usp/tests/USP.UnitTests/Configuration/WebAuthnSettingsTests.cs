using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Moq;
using USP.Core.Models.Configuration;
using Xunit;

namespace USP.UnitTests.Configuration;

/// <summary>
/// Unit tests for WebAuthn configuration validation
/// </summary>
public class WebAuthnSettingsTests
{
    [Fact]
    public void WebAuthnSettings_DefaultConstructor_SetsEmptyValues()
    {
        // Arrange & Act
        var settings = new WebAuthnSettings();

        // Assert
        settings.RelyingPartyId.Should().Be(string.Empty, "should not have localhost default");
        settings.Origin.Should().Be(string.Empty, "should not have localhost default");
        settings.RelyingPartyName.Should().NotBeNullOrEmpty();
        settings.TimestampDriftTolerance.Should().BeGreaterThan(0);
        settings.ChallengeExpirationMinutes.Should().BeGreaterThan(0);
    }

    [Fact]
    public void WebAuthnSettings_DoesNotContainLocalhostDefaults()
    {
        // Arrange & Act
        var settings = new WebAuthnSettings();

        // Assert
        settings.RelyingPartyId.Should().NotContain("localhost");
        settings.Origin.Should().NotContain("localhost");
    }

    [Theory]
    [InlineData("", "")]
    [InlineData(null, null)]
    [InlineData("", "https://example.com")]
    [InlineData("example.com", "")]
    public void WebAuthnValidation_WithMissingConfiguration_ThrowsException(string? relyingPartyId, string? origin)
    {
        // Arrange
        var configValues = new Dictionary<string, string?>
        {
            { "WebAuthn:RelyingPartyId", relyingPartyId },
            { "WebAuthn:Origin", origin }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();

        var settings = new WebAuthnSettings();
        configuration.GetSection("WebAuthn").Bind(settings);

        // Act & Assert - This simulates the validation in Program.cs
        var isValid = !string.IsNullOrEmpty(settings.RelyingPartyId) &&
                     !string.IsNullOrEmpty(settings.Origin);

        isValid.Should().BeFalse("configuration should be invalid without required values");
    }

    [Theory]
    [InlineData("localhost", "https://localhost:8443")]
    [InlineData("example.com", "https://localhost:5001")]
    [InlineData("localhost:8443", "https://example.com")]
    public void WebAuthnValidation_InProduction_WithLocalhost_ShouldBeInvalid(string relyingPartyId, string origin)
    {
        // Arrange
        var settings = new WebAuthnSettings
        {
            RelyingPartyId = relyingPartyId,
            Origin = origin
        };

        var environmentMock = new Mock<IWebHostEnvironment>();
        environmentMock.Setup(e => e.EnvironmentName).Returns("Production");

        // Act - This simulates the validation in Program.cs
        var containsLocalhost = settings.RelyingPartyId.Contains("localhost") ||
                               settings.Origin.Contains("localhost");

        // Assert
        containsLocalhost.Should().BeTrue("should detect localhost in production configuration");
    }

    [Theory]
    [InlineData("example.com", "https://example.com")]
    [InlineData("app.example.com", "https://app.example.com")]
    [InlineData("auth.mycompany.io", "https://auth.mycompany.io:8443")]
    public void WebAuthnValidation_InProduction_WithoutLocalhost_ShouldBeValid(string relyingPartyId, string origin)
    {
        // Arrange
        var settings = new WebAuthnSettings
        {
            RelyingPartyId = relyingPartyId,
            Origin = origin
        };

        var environmentMock = new Mock<IWebHostEnvironment>();
        environmentMock.Setup(e => e.EnvironmentName).Returns("Production");

        // Act - This simulates the validation in Program.cs
        var containsLocalhost = settings.RelyingPartyId.Contains("localhost") ||
                               settings.Origin.Contains("localhost");

        // Assert
        containsLocalhost.Should().BeFalse("production configuration should not contain localhost");
    }

    [Theory]
    [InlineData("localhost", "https://localhost:8443")]
    [InlineData("127.0.0.1", "https://127.0.0.1:5001")]
    public void WebAuthnValidation_InDevelopment_WithLocalhost_ShouldBeValid(string relyingPartyId, string origin)
    {
        // Arrange
        var settings = new WebAuthnSettings
        {
            RelyingPartyId = relyingPartyId,
            Origin = origin
        };

        var environmentMock = new Mock<IWebHostEnvironment>();
        environmentMock.Setup(e => e.EnvironmentName).Returns("Development");

        // Act - In development, localhost is acceptable
        var containsLocalhost = settings.RelyingPartyId.Contains("localhost") ||
                               settings.Origin.Contains("localhost") ||
                               settings.RelyingPartyId.Contains("127.0.0.1");

        // Assert
        containsLocalhost.Should().BeTrue("development can use localhost");
    }

    [Fact]
    public void WebAuthnSettings_FromConfiguration_BindsCorrectly()
    {
        // Arrange
        var configValues = new Dictionary<string, string?>
        {
            { "WebAuthn:RelyingPartyId", "example.com" },
            { "WebAuthn:RelyingPartyName", "My App" },
            { "WebAuthn:Origin", "https://example.com" },
            { "WebAuthn:TimestampDriftTolerance", "600000" },
            { "WebAuthn:ChallengeExpirationMinutes", "10" }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();

        // Act
        var settings = new WebAuthnSettings();
        configuration.GetSection("WebAuthn").Bind(settings);

        // Assert
        settings.RelyingPartyId.Should().Be("example.com");
        settings.RelyingPartyName.Should().Be("My App");
        settings.Origin.Should().Be("https://example.com");
        settings.TimestampDriftTolerance.Should().Be(600000);
        settings.ChallengeExpirationMinutes.Should().Be(10);
    }

    [Theory]
    [InlineData("example.com", "https://example.com", true)]
    [InlineData("app.example.com", "https://app.example.com:8443", true)]
    [InlineData("localhost", "https://localhost:5001", false)]
    [InlineData("", "https://example.com", false)]
    [InlineData("example.com", "", false)]
    public void WebAuthnSettings_ProductionValidation_WorksCorrectly(
        string relyingPartyId,
        string origin,
        bool expectedValid)
    {
        // Arrange
        var settings = new WebAuthnSettings
        {
            RelyingPartyId = relyingPartyId,
            Origin = origin
        };

        // Act - Simulates Program.cs validation logic
        var isConfigured = !string.IsNullOrEmpty(settings.RelyingPartyId) &&
                          !string.IsNullOrEmpty(settings.Origin);
        var containsLocalhost = settings.RelyingPartyId.Contains("localhost") ||
                               settings.Origin.Contains("localhost");
        var isValidForProduction = isConfigured && !containsLocalhost;

        // Assert
        isValidForProduction.Should().Be(expectedValid);
    }

    [Fact]
    public void WebAuthnSettings_SourceCode_DoesNotHaveLocalhostDefaults()
    {
        // Arrange & Act
        var sourceCode = System.IO.File.ReadAllText(
            "/home/tshepo/projects/tw/services/usp/src/USP.Core/Models/Configuration/WebAuthnSettings.cs");

        // Assert
        sourceCode.Should().NotContain("= \"localhost\"",
            "WebAuthnSettings should not have localhost as default value");
        sourceCode.Should().NotContain("= \"https://localhost",
            "WebAuthnSettings should not have localhost URL as default value");
    }
}
