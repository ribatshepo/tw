using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace USP.IntegrationTests.Observability;

/// <summary>
/// Integration tests for health check endpoints.
/// Validates that health checks properly report system status.
/// </summary>
public class HealthChecksTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public HealthChecksTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task HealthEndpoint_ShouldReturnOk()
    {
        // Act
        var response = await _client.GetAsync("/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task HealthEndpoint_ShouldReturnJsonResponse()
    {
        // Act
        var response = await _client.GetAsync("/health");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");

        var healthReport = JsonDocument.Parse(content);
        healthReport.RootElement.TryGetProperty("status", out var status).Should().BeTrue();
        healthReport.RootElement.TryGetProperty("checks", out var checks).Should().BeTrue();
        healthReport.RootElement.TryGetProperty("totalDuration", out var duration).Should().BeTrue();
    }

    [Fact]
    public async Task HealthEndpoint_ShouldIncludePostgreSqlCheck()
    {
        // Act
        var response = await _client.GetAsync("/health");
        var content = await response.Content.ReadAsStringAsync();
        var healthReport = JsonDocument.Parse(content);

        // Assert
        var checks = healthReport.RootElement.GetProperty("checks");
        var checksList = checks.EnumerateArray().Select(c => c.GetProperty("name").GetString()).ToList();

        checksList.Should().Contain("postgresql");
    }

    [Fact]
    public async Task HealthEndpoint_ShouldIncludeRedisCheck()
    {
        // Act
        var response = await _client.GetAsync("/health");
        var content = await response.Content.ReadAsStringAsync();
        var healthReport = JsonDocument.Parse(content);

        // Assert
        var checks = healthReport.RootElement.GetProperty("checks");
        var checksList = checks.EnumerateArray().Select(c => c.GetProperty("name").GetString()).ToList();

        checksList.Should().Contain("redis");
    }

    [Fact]
    public async Task HealthLiveEndpoint_ShouldReturnOk()
    {
        // Act
        var response = await _client.GetAsync("/health/live");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task HealthReadyEndpoint_ShouldReturnOk()
    {
        // Act
        var response = await _client.GetAsync("/health/ready");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task HealthEndpoint_ShouldIncludeDetailedCheck()
    {
        // Act
        var response = await _client.GetAsync("/health");
        var content = await response.Content.ReadAsStringAsync();
        var healthReport = JsonDocument.Parse(content);

        // Assert
        var checks = healthReport.RootElement.GetProperty("checks");
        var checksList = checks.EnumerateArray().Select(c => c.GetProperty("name").GetString()).ToList();

        checksList.Should().Contain("detailed");
    }

    [Fact]
    public async Task HealthEndpoint_ShouldReportOverallStatus()
    {
        // Act
        var response = await _client.GetAsync("/health");
        var content = await response.Content.ReadAsStringAsync();
        var healthReport = JsonDocument.Parse(content);

        // Assert
        var status = healthReport.RootElement.GetProperty("status").GetString();
        status.Should().BeOneOf("Healthy", "Degraded", "Unhealthy");
    }

    [Fact]
    public async Task HealthEndpoint_ChecksShouldHaveStatus()
    {
        // Act
        var response = await _client.GetAsync("/health");
        var content = await response.Content.ReadAsStringAsync();
        var healthReport = JsonDocument.Parse(content);

        // Assert
        var checks = healthReport.RootElement.GetProperty("checks").EnumerateArray();
        foreach (var check in checks)
        {
            check.TryGetProperty("status", out var status).Should().BeTrue();
            status.GetString().Should().BeOneOf("Healthy", "Degraded", "Unhealthy");
        }
    }

    [Fact]
    public async Task HealthEndpoint_ShouldIncludeDuration()
    {
        // Act
        var response = await _client.GetAsync("/health");
        var content = await response.Content.ReadAsStringAsync();
        var healthReport = JsonDocument.Parse(content);

        // Assert
        var totalDuration = healthReport.RootElement.GetProperty("totalDuration").GetDouble();
        totalDuration.Should().BeGreaterThan(0);
    }
}
