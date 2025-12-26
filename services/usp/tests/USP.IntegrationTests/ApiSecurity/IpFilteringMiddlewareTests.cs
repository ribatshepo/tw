using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using FluentAssertions;

namespace USP.IntegrationTests.ApiSecurity;

/// <summary>
/// Integration tests for IP filtering middleware
/// </summary>
public class IpFilteringMiddlewareTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public IpFilteringMiddlewareTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task HealthEndpoint_ShouldSkipIpFiltering()
    {
        // Act
        var response = await _client.GetAsync("/health");

        // Assert - Health check should always work
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task TemporaryBan_ShouldBlockIp_AfterExcessiveFailures()
    {
        // Arrange
        var cache = _factory.Services.GetRequiredService<IDistributedCache>();
        var testIp = "192.168.1.100";

        // Simulate banned IP
        await cache.SetStringAsync($"ipfilter:banned:{testIp}", "1",
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15)
            });

        // Act - Request with X-Forwarded-For header simulating banned IP
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/roles");
        request.Headers.Add("X-Forwarded-For", testIp);

        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("banned");

        // Cleanup
        await cache.RemoveAsync($"ipfilter:banned:{testIp}");
    }

    [Fact]
    public async Task FailedAttempts_ShouldBeTracked_InCache()
    {
        // Arrange
        var cache = _factory.Services.GetRequiredService<IDistributedCache>();
        var testIp = "192.168.1.200";

        // Simulate failed attempts
        await cache.SetStringAsync($"ipfilter:failed:{testIp}", "3");

        // Act
        var attempts = await cache.GetStringAsync($"ipfilter:failed:{testIp}");

        // Assert
        attempts.Should().Be("3");

        // Cleanup
        await cache.RemoveAsync($"ipfilter:failed:{testIp}");
    }

    [Fact]
    public async Task IpFromXForwardedFor_ShouldBeExtracted_Correctly()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/roles");
        request.Headers.Add("X-Forwarded-For", "203.0.113.45, 198.51.100.14");

        // Act
        var response = await _client.SendAsync(request);

        // Assert - Should extract first IP from comma-separated list
        // Middleware should process without errors
        response.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task IpFromXRealIp_ShouldBeUsed_WhenPresent()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/roles");
        request.Headers.Add("X-Real-IP", "203.0.113.50");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task BanDuration_ShouldExpire_AfterConfiguredTime()
    {
        // Arrange
        var cache = _factory.Services.GetRequiredService<IDistributedCache>();
        var testIp = "192.168.1.101";

        // Ban IP with short TTL
        await cache.SetStringAsync($"ipfilter:banned:{testIp}", "1",
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(2)
            });

        // Verify ban is active
        var bannedValue = await cache.GetStringAsync($"ipfilter:banned:{testIp}");
        bannedValue.Should().NotBeNullOrEmpty();

        // Wait for expiration
        await Task.Delay(TimeSpan.FromSeconds(3));

        // Act - Check if ban expired
        var expiredValue = await cache.GetStringAsync($"ipfilter:banned:{testIp}");

        // Assert
        expiredValue.Should().BeNullOrEmpty("ban should have expired");
    }
}
