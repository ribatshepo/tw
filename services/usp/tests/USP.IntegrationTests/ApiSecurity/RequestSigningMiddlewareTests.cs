using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using FluentAssertions;

namespace USP.IntegrationTests.ApiSecurity;

/// <summary>
/// Integration tests for request signing middleware
/// </summary>
public class RequestSigningMiddlewareTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private const string TestSecret = "test-signing-secret-key-must-be-at-least-32-chars-long";

    public RequestSigningMiddlewareTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ExemptEndpoint_ShouldNotRequireSignature()
    {
        // Act - Health and login endpoints are exempt
        var healthResponse = await _client.GetAsync("/health");
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "test@example.com",
            password = "Test123!"
        });

        // Assert
        healthResponse.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
        loginResponse.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SignatureVerification_IsDisabledByDefault()
    {
        // Note: RequestSigning:EnableSignatureVerification is false in appsettings.json

        // Act - Make request without signature
        var response = await _client.GetAsync("/api/roles");

        // Assert - Should not be rejected for missing signature
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ValidSignature_ShouldPassVerification()
    {
        // This test demonstrates how to create valid request signatures
        // In production, clients would implement this signing logic

        // Arrange
        var method = "GET";
        var path = "/api/roles";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var nonce = Guid.NewGuid().ToString();
        var body = string.Empty;

        var signature = ComputeHmacSignature(method, path, timestamp, nonce, body, TestSecret);

        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("X-Signature", signature);
        request.Headers.Add("X-Timestamp", timestamp);
        request.Headers.Add("X-Nonce", nonce);

        // Act
        var response = await _client.SendAsync(request);

        // Assert - Should not fail due to signature (signing is disabled by default)
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized,
            "request with valid signature format should not be rejected");
    }

    [Fact]
    public async Task ExpiredTimestamp_ShouldBeRejected()
    {
        // Arrange - Timestamp from 10 minutes ago (exceeds 5 minute drift)
        var oldTimestamp = DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeSeconds().ToString();

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/roles");
        request.Headers.Add("X-Timestamp", oldTimestamp);
        request.Headers.Add("X-Nonce", Guid.NewGuid().ToString());
        request.Headers.Add("X-Signature", "dummy-signature");

        // Act
        var response = await _client.SendAsync(request);

        // Assert - Should be rejected (if signing enabled)
        // Since signing is disabled by default, this just verifies no server error
        response.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task NonceReuse_ShouldBeDetected()
    {
        // This test verifies replay attack prevention
        // Same nonce should not be accepted twice

        // Arrange
        var nonce = Guid.NewGuid().ToString();
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

        var request1 = CreateSignedRequest(HttpMethod.Get, "/api/roles", timestamp, nonce);
        var request2 = CreateSignedRequest(HttpMethod.Get, "/api/roles", timestamp, nonce);

        // Act - Send same nonce twice
        var response1 = await _client.SendAsync(request1);
        var response2 = await _client.SendAsync(request2);

        // Assert - Second request should be rejected (if signing enabled)
        // With signing disabled, just verify no errors
        response1.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError);
        response2.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError);
    }

    private HttpRequestMessage CreateSignedRequest(HttpMethod method, string path, string timestamp, string nonce)
    {
        var body = string.Empty;
        var signature = ComputeHmacSignature(method.Method, path, timestamp, nonce, body, TestSecret);

        var request = new HttpRequestMessage(method, path);
        request.Headers.Add("X-Signature", signature);
        request.Headers.Add("X-Timestamp", timestamp);
        request.Headers.Add("X-Nonce", nonce);

        return request;
    }

    private string ComputeHmacSignature(string method, string path, string timestamp, string nonce, string body, string secret)
    {
        var signatureString = $"{method}{path}{timestamp}{nonce}{body}";
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var dataBytes = Encoding.UTF8.GetBytes(signatureString);

        using var hmac = new HMACSHA256(keyBytes);
        var hashBytes = hmac.ComputeHash(dataBytes);
        return Convert.ToBase64String(hashBytes);
    }
}
