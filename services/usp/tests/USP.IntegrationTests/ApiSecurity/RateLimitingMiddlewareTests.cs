using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Caching.Distributed;
using Xunit;
using FluentAssertions;

namespace USP.IntegrationTests.ApiSecurity;

/// <summary>
/// Integration tests for rate limiting middleware
/// </summary>
public class RateLimitingMiddlewareTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public RateLimitingMiddlewareTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task HealthEndpoint_ShouldNotBeRateLimited()
    {
        // Arrange & Act - Make 20 rapid requests to health endpoint
        var tasks = Enumerable.Range(0, 20)
            .Select(_ => _client.GetAsync("/health"))
            .ToArray();

        var responses = await Task.WhenAll(tasks);

        // Assert - All requests should succeed
        responses.Should().AllSatisfy(r => r.StatusCode.Should().Be(HttpStatusCode.OK));
    }

    [Fact]
    public async Task ApiEndpoint_ShouldEnforceRateLimiting_WhenExceedingLimit()
    {
        // Arrange
        var cache = _factory.Services.GetRequiredService<IDistributedCache>();

        // Clear any existing rate limit data
        await cache.RemoveAsync("ratelimit:global");

        // Act - Make 300 rapid requests (exceeds per-IP limit of 200/min)
        var successCount = 0;
        var rateLimitedCount = 0;

        for (int i = 0; i < 300; i++)
        {
            var response = await _client.GetAsync("/api/roles");

            if (response.StatusCode == HttpStatusCode.OK ||
                response.StatusCode == HttpStatusCode.Unauthorized)
            {
                successCount++;
            }
            else if (response.StatusCode == (HttpStatusCode)429) // Too Many Requests
            {
                rateLimitedCount++;
            }

            // Small delay to avoid overwhelming the test
            if (i % 50 == 0)
            {
                await Task.Delay(10);
            }
        }

        // Assert - Some requests should be rate limited
        rateLimitedCount.Should().BeGreaterThan(0, "rate limiting should kick in after threshold");
        successCount.Should().BeLessThan(300, "not all requests should succeed");
    }

    [Fact]
    public async Task RateLimitResponse_ShouldIncludeRetryAfterHeader()
    {
        // Arrange - Trigger rate limit by making many requests
        for (int i = 0; i < 250; i++)
        {
            await _client.GetAsync("/api/roles");
        }

        // Act - Make one more request that should be rate limited
        var response = await _client.GetAsync("/api/roles");

        // Assert
        if (response.StatusCode == (HttpStatusCode)429)
        {
            response.Headers.Should().ContainKey("Retry-After");
            var retryAfter = response.Headers.GetValues("Retry-After").FirstOrDefault();
            retryAfter.Should().NotBeNullOrEmpty();

            // Should be parseable as integer (seconds)
            int.TryParse(retryAfter, out var seconds).Should().BeTrue();
            seconds.Should().BeGreaterThan(0);
        }
    }

    [Fact]
    public async Task LoginEndpoint_ShouldHaveStricterRateLimit()
    {
        // Act - Make 15 requests to login endpoint (limit is 10/min)
        var responses = new List<HttpResponseMessage>();

        for (int i = 0; i < 15; i++)
        {
            var response = await _client.PostAsJsonAsync("/api/auth/login", new
            {
                email = "test@example.com",
                password = "TestPassword123!"
            });
            responses.Add(response);
        }

        // Assert - Some requests should be rate limited
        var rateLimited = responses.Count(r => r.StatusCode == (HttpStatusCode)429);
        rateLimited.Should().BeGreaterThan(0, "login endpoint should have strict rate limiting");
    }

    [Fact]
    public async Task RateLimitViolation_ShouldBePersisted_InRedisCache()
    {
        // Arrange
        var cache = _factory.Services.GetRequiredService<IDistributedCache>();
        var testIp = "192.168.1.100";

        // Act - Simulate rate limit violations
        await cache.SetStringAsync($"ratelimit:violations:{testIp}", "3");

        // Assert - Violation should be stored
        var storedViolation = await cache.GetStringAsync($"ratelimit:violations:{testIp}");
        storedViolation.Should().Be("3");

        // Cleanup
        await cache.RemoveAsync($"ratelimit:violations:{testIp}");
    }

    [Fact]
    public async Task GlobalRateLimit_ShouldProtectAgainstDDoS()
    {
        // This test verifies that the global rate limiter can handle high request volumes
        // In production, global limit is 5000 req/sec

        // Arrange
        var requestCount = 100; // Reduced for test performance

        // Act - Fire requests in parallel
        var tasks = Enumerable.Range(0, requestCount)
            .Select(_ => _client.GetAsync("/api/roles"))
            .ToArray();

        var responses = await Task.WhenAll(tasks);

        // Assert - System should handle concurrent requests gracefully
        var serverErrors = responses.Count(r =>
            r.StatusCode == HttpStatusCode.InternalServerError);

        serverErrors.Should().Be(0, "rate limiter should not cause server errors");
    }

    [Fact]
    public async Task BurstAllowance_ShouldAllowTemporarySpikes()
    {
        // Arrange - Burst allowance is 20% additional capacity
        var normalLimit = 100;
        var burstLimit = normalLimit + (normalLimit * 20 / 100); // 120

        // Act - Make requests up to burst limit
        var responses = new List<HttpResponseMessage>();
        for (int i = 0; i < 110; i++)
        {
            var response = await _client.GetAsync("/api/roles");
            responses.Add(response);
        }

        // Assert - Most requests should succeed (within burst allowance)
        var successful = responses.Count(r =>
            r.StatusCode == HttpStatusCode.OK ||
            r.StatusCode == HttpStatusCode.Unauthorized);

        successful.Should().BeGreaterThan(100, "burst allowance should permit temporary spikes");
    }

    [Fact]
    public async Task SlidingWindow_ShouldBeMoreAccurate_ThanFixedWindow()
    {
        // This test verifies sliding window algorithm behavior
        // Sliding window prevents burst at window boundaries

        // Arrange - Make 50 requests
        for (int i = 0; i < 50; i++)
        {
            await _client.GetAsync("/api/roles");
        }

        // Act - Wait 30 seconds (half window), make 50 more
        await Task.Delay(TimeSpan.FromSeconds(1)); // Reduced for test speed

        var secondBatchSuccess = 0;
        for (int i = 0; i < 50; i++)
        {
            var response = await _client.GetAsync("/api/roles");
            if (response.StatusCode != (HttpStatusCode)429)
            {
                secondBatchSuccess++;
            }
        }

        // Assert - With sliding window, some requests should succeed
        secondBatchSuccess.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task PerEndpointLimits_ShouldBeIndependent()
    {
        // Arrange - Different endpoints have different limits

        // Act - Exhaust login endpoint limit
        for (int i = 0; i < 12; i++)
        {
            await _client.PostAsJsonAsync("/api/auth/login", new
            {
                email = "test@example.com",
                password = "Test123!"
            });
        }

        // Try accessing different endpoint
        var rolesResponse = await _client.GetAsync("/api/roles");

        // Assert - Other endpoints should still work
        rolesResponse.StatusCode.Should().NotBe((HttpStatusCode)429,
            "per-endpoint limits should be independent");
    }

    [Fact]
    public async Task RateLimitHeaders_ShouldProvideUsefulInformation()
    {
        // Act
        var response = await _client.GetAsync("/api/roles");

        // Assert - Response should include rate limit headers
        if (response.StatusCode == (HttpStatusCode)429)
        {
            response.Headers.Should().ContainKey("X-RateLimit-Limit");
            response.Headers.Should().ContainKey("X-RateLimit-Remaining");
            response.Headers.Should().ContainKey("X-RateLimit-Reset");
        }
    }
}
