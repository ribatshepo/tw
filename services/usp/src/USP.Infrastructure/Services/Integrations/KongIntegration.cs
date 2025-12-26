using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using USP.Core.Services.Integrations;

namespace USP.Infrastructure.Services.Integrations;

/// <summary>
/// Kong API Gateway integration
/// </summary>
public class KongIntegration : IApiGatewayIntegration
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<KongIntegration> _logger;
    private readonly string _adminApiUrl;

    public KongIntegration(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<KongIntegration> logger)
    {
        _httpClient = httpClientFactory.CreateClient("Kong");
        _logger = logger;
        _adminApiUrl = configuration.GetValue<string>("ApiGateway:Kong:AdminUrl", "http://kong-admin:8001");

        _logger.LogInformation("Kong Integration initialized with Admin URL: {AdminUrl}", _adminApiUrl);
    }

    public async Task<bool> RegisterServiceAsync(ServiceRegistration registration)
    {
        try
        {
            _logger.LogInformation("Registering service with Kong: {ServiceName}", registration.ServiceName);

            var servicePayload = new
            {
                name = registration.ServiceId,
                url = $"{registration.Protocol}://{registration.Host}:{registration.Port}{registration.Path}",
                retries = 5,
                connect_timeout = 60000,
                write_timeout = 60000,
                read_timeout = 60000,
                tags = registration.Tags.Select(kvp => $"{kvp.Key}:{kvp.Value}").ToArray()
            };

            var response = await _httpClient.PostAsJsonAsync($"{_adminApiUrl}/services", servicePayload);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Service registered successfully with Kong: {ServiceId}", registration.ServiceId);
                return true;
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("Failed to register service with Kong: {StatusCode}, {Error}",
                response.StatusCode, errorContent);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering service with Kong: {ServiceName}", registration.ServiceName);
            return false;
        }
    }

    public async Task<bool> DeregisterServiceAsync(string serviceId)
    {
        try
        {
            _logger.LogInformation("Deregistering service from Kong: {ServiceId}", serviceId);

            var response = await _httpClient.DeleteAsync($"{_adminApiUrl}/services/{serviceId}");

            if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogInformation("Service deregistered successfully from Kong: {ServiceId}", serviceId);
                return true;
            }

            _logger.LogWarning("Failed to deregister service from Kong: {StatusCode}", response.StatusCode);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deregistering service from Kong: {ServiceId}", serviceId);
            return false;
        }
    }

    public async Task<bool> UpdateRoutesAsync(string serviceId, List<RouteConfiguration> routes)
    {
        try
        {
            _logger.LogInformation("Updating routes for service in Kong: {ServiceId}", serviceId);

            foreach (var route in routes)
            {
                var routePayload = new
                {
                    name = route.Name,
                    paths = route.Paths.ToArray(),
                    methods = route.Methods.ToArray(),
                    strip_path = route.StripPath == "true",
                    preserve_host = route.PreserveHost,
                    service = new { id = serviceId }
                };

                var response = await _httpClient.PostAsJsonAsync($"{_adminApiUrl}/routes", routePayload);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("Failed to create route in Kong: {StatusCode}, {Error}",
                        response.StatusCode, errorContent);
                    return false;
                }
            }

            _logger.LogInformation("Routes updated successfully for service in Kong: {ServiceId}", serviceId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating routes for service in Kong: {ServiceId}", serviceId);
            return false;
        }
    }

    public async Task<bool> ConfigureAuthenticationAsync(string serviceId, AuthenticationConfig config)
    {
        try
        {
            _logger.LogInformation("Configuring authentication for service in Kong: {ServiceId}, Type: {Type}",
                serviceId, config.Type);

            var pluginName = config.Type.ToLower() switch
            {
                "jwt" => "jwt",
                "oauth2" => "oauth2",
                "api-key" => "key-auth",
                "mtls" => "mtls-auth",
                _ => "jwt"
            };

            var pluginPayload = new
            {
                name = pluginName,
                service = new { id = serviceId },
                config = config.Config
            };

            var response = await _httpClient.PostAsJsonAsync($"{_adminApiUrl}/plugins", pluginPayload);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Authentication configured successfully for service in Kong: {ServiceId}", serviceId);
                return true;
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("Failed to configure authentication in Kong: {StatusCode}, {Error}",
                response.StatusCode, errorContent);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error configuring authentication for service in Kong: {ServiceId}", serviceId);
            return false;
        }
    }

    public async Task<bool> ConfigureRateLimitingAsync(string serviceId, RateLimitConfig config)
    {
        try
        {
            _logger.LogInformation("Configuring rate limiting for service in Kong: {ServiceId}", serviceId);

            var pluginPayload = new
            {
                name = "rate-limiting",
                service = new { id = serviceId },
                config = new
                {
                    second = config.RequestsPerSecond > 0 ? config.RequestsPerSecond : (int?)null,
                    minute = config.RequestsPerMinute > 0 ? config.RequestsPerMinute : (int?)null,
                    hour = config.RequestsPerHour > 0 ? config.RequestsPerHour : (int?)null,
                    policy = config.Policy
                }
            };

            var response = await _httpClient.PostAsJsonAsync($"{_adminApiUrl}/plugins", pluginPayload);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Rate limiting configured successfully for service in Kong: {ServiceId}", serviceId);
                return true;
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("Failed to configure rate limiting in Kong: {StatusCode}, {Error}",
                response.StatusCode, errorContent);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error configuring rate limiting for service in Kong: {ServiceId}", serviceId);
            return false;
        }
    }

    public async Task<bool> ConfigureIpFilteringAsync(string serviceId, IpFilterConfig config)
    {
        try
        {
            _logger.LogInformation("Configuring IP filtering for service in Kong: {ServiceId}", serviceId);

            var pluginPayload = new
            {
                name = "ip-restriction",
                service = new { id = serviceId },
                config = new
                {
                    allow = config.Whitelist.Any() ? config.Whitelist.ToArray() : null,
                    deny = config.Blacklist.Any() ? config.Blacklist.ToArray() : null
                }
            };

            var response = await _httpClient.PostAsJsonAsync($"{_adminApiUrl}/plugins", pluginPayload);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("IP filtering configured successfully for service in Kong: {ServiceId}", serviceId);
                return true;
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("Failed to configure IP filtering in Kong: {StatusCode}, {Error}",
                response.StatusCode, errorContent);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error configuring IP filtering for service in Kong: {ServiceId}", serviceId);
            return false;
        }
    }

    public async Task<ServiceHealthStatus> GetServiceHealthAsync(string serviceId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_adminApiUrl}/services/{serviceId}/health");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var healthData = JsonSerializer.Deserialize<JsonElement>(content);

                return new ServiceHealthStatus
                {
                    ServiceId = serviceId,
                    Status = healthData.TryGetProperty("data", out var data) &&
                             data.TryGetProperty("health", out var health)
                        ? health.GetString() ?? "unknown"
                        : "unknown",
                    LastCheck = DateTime.UtcNow
                };
            }

            return new ServiceHealthStatus
            {
                ServiceId = serviceId,
                Status = "unknown",
                LastCheck = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting service health from Kong: {ServiceId}", serviceId);
            return new ServiceHealthStatus
            {
                ServiceId = serviceId,
                Status = "unknown",
                LastCheck = DateTime.UtcNow
            };
        }
    }

    public async Task<GatewayMetrics> GetMetricsAsync(string serviceId, DateTime? startTime = null, DateTime? endTime = null)
    {
        try
        {
            _logger.LogDebug("Getting metrics for service from Kong: {ServiceId}", serviceId);

            return new GatewayMetrics
            {
                ServiceId = serviceId,
                TotalRequests = 0,
                SuccessfulRequests = 0,
                FailedRequests = 0,
                AverageLatencyMs = 0,
                P95LatencyMs = 0,
                P99LatencyMs = 0,
                StatusCodes = new Dictionary<int, long>()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting metrics from Kong: {ServiceId}", serviceId);
            return new GatewayMetrics { ServiceId = serviceId };
        }
    }
}
