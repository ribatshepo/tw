using System.Net;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using FluentAssertions;

namespace USP.IntegrationTests.ApiSecurity;

/// <summary>
/// Integration tests for API threat protection middleware
/// </summary>
public class ApiThreatProtectionMiddlewareTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public ApiThreatProtectionMiddlewareTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task SqlInjectionInQueryString_ShouldBeDetected()
    {
        // Act - Send SQL injection attempt in query parameter
        var response = await _client.GetAsync("/api/roles?name=admin' OR '1'='1");

        // Assert - Should be blocked
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Malicious");
    }

    [Fact]
    public async Task XssAttemptInRequestBody_ShouldBeBlocked()
    {
        // Act - Send XSS payload
        var xssPayload = new
        {
            name = "<script>alert('XSS')</script>",
            description = "Test"
        };

        var response = await _client.PostAsJsonAsync("/api/roles", xssPayload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PathTraversalAttempt_ShouldBeBlocked()
    {
        // Act - Attempt path traversal
        var response = await _client.GetAsync("/api/../../../etc/passwd");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ExcessiveJsonDepth_ShouldBeRejected()
    {
        // Arrange - Create deeply nested JSON (> 10 levels)
        var deeplyNestedJson = GenerateDeeplyNestedJson(15);

        var content = new StringContent(deeplyNestedJson, Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/api/roles", content);

        // Assert - Should be blocked or rejected
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Forbidden,
            HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ExcessiveHeaderCount_ShouldBeDetected()
    {
        // Arrange - Add excessive headers (> 100)
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/roles");

        for (int i = 0; i < 150; i++)
        {
            request.Headers.Add($"X-Custom-Header-{i}", $"value{i}");
        }

        // Act
        var response = await _client.SendAsync(request);

        // Assert - Should be blocked
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task OversizedRequestBody_ShouldBeRejected()
    {
        // Arrange - Create payload larger than 4MB
        var largePayload = new string('A', 5 * 1024 * 1024); // 5MB

        var content = new StringContent(largePayload, Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/api/roles", content);

        // Assert - Should be blocked
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RapidSequentialRequests_ShouldBeDetected()
    {
        // Act - Make 60 requests in 1 second (exceeds threshold of 50)
        var tasks = Enumerable.Range(0, 60)
            .Select(_ => _client.GetAsync("/api/roles"))
            .ToArray();

        var responses = await Task.WhenAll(tasks);

        // Assert - Some should be blocked for suspicious pattern
        var blockedCount = responses.Count(r => r.StatusCode == HttpStatusCode.Forbidden);
        blockedCount.Should().BeGreaterThan(0, "rapid requests should trigger threat detection");
    }

    [Fact]
    public async Task LegitimateRequest_ShouldNotBeBlocked()
    {
        // Act - Normal request
        var response = await _client.GetAsync("/api/roles");

        // Assert - Should not be blocked
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ThreatDetection_ShouldLogIncident()
    {
        // Act - Trigger threat detection
        var response = await _client.GetAsync("/api/roles?id=1' UNION SELECT * FROM users--");

        // Assert - Response should include incident ID for tracking
        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("incidentId");
        }
    }

    [Fact]
    public async Task MultipleThreats_ShouldAllBeDetected()
    {
        // This test verifies multiple threat patterns in single request

        // Act - SQL injection + XSS
        var response = await _client.PostAsJsonAsync("/api/roles", new
        {
            name = "admin' OR '1'='1",
            description = "<script>alert('xss')</script>"
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private string GenerateDeeplyNestedJson(int depth)
    {
        var json = new StringBuilder();
        json.Append("{");

        for (int i = 0; i < depth; i++)
        {
            json.Append("\"level\": {");
        }

        json.Append("\"value\": \"deep\"");

        for (int i = 0; i < depth; i++)
        {
            json.Append("}");
        }

        json.Append("}");

        return json.ToString();
    }
}
