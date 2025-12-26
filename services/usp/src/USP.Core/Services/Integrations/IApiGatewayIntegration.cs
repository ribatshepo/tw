namespace USP.Core.Services.Integrations;

/// <summary>
/// API Gateway integration for centralized security policy enforcement
/// </summary>
public interface IApiGatewayIntegration
{
    /// <summary>
    /// Register service with API gateway
    /// </summary>
    Task<bool> RegisterServiceAsync(ServiceRegistration registration);

    /// <summary>
    /// Deregister service from API gateway
    /// </summary>
    Task<bool> DeregisterServiceAsync(string serviceId);

    /// <summary>
    /// Update service routes
    /// </summary>
    Task<bool> UpdateRoutesAsync(string serviceId, List<RouteConfiguration> routes);

    /// <summary>
    /// Configure authentication plugin
    /// </summary>
    Task<bool> ConfigureAuthenticationAsync(string serviceId, AuthenticationConfig config);

    /// <summary>
    /// Configure rate limiting
    /// </summary>
    Task<bool> ConfigureRateLimitingAsync(string serviceId, RateLimitConfig config);

    /// <summary>
    /// Configure IP filtering
    /// </summary>
    Task<bool> ConfigureIpFilteringAsync(string serviceId, IpFilterConfig config);

    /// <summary>
    /// Get service health status from gateway
    /// </summary>
    Task<ServiceHealthStatus> GetServiceHealthAsync(string serviceId);

    /// <summary>
    /// Get gateway metrics
    /// </summary>
    Task<GatewayMetrics> GetMetricsAsync(string serviceId, DateTime? startTime = null, DateTime? endTime = null);
}

public class ServiceRegistration
{
    public string ServiceId { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }
    public string Protocol { get; set; } = "http";
    public string Path { get; set; } = "/";
    public Dictionary<string, string> Tags { get; set; } = new();
    public int HealthCheckInterval { get; set; } = 30;
}

public class RouteConfiguration
{
    public string Name { get; set; } = string.Empty;
    public List<string> Paths { get; set; } = new();
    public List<string> Methods { get; set; } = new();
    public string StripPath { get; set; } = "false";
    public bool PreserveHost { get; set; } = true;
}

public class AuthenticationConfig
{
    public string Type { get; set; } = "jwt"; // jwt, oauth2, api-key, mtls
    public Dictionary<string, string> Config { get; set; } = new();
}

public class RateLimitConfig
{
    public int RequestsPerSecond { get; set; }
    public int RequestsPerMinute { get; set; }
    public int RequestsPerHour { get; set; }
    public string Policy { get; set; } = "local"; // local, cluster, redis
}

public class IpFilterConfig
{
    public List<string> Whitelist { get; set; } = new();
    public List<string> Blacklist { get; set; } = new();
    public string DefaultAction { get; set; } = "allow"; // allow, deny
}

public class ServiceHealthStatus
{
    public string ServiceId { get; set; } = string.Empty;
    public string Status { get; set; } = "unknown"; // healthy, unhealthy, unknown
    public int HealthyInstances { get; set; }
    public int UnhealthyInstances { get; set; }
    public DateTime LastCheck { get; set; }
}

public class GatewayMetrics
{
    public string ServiceId { get; set; } = string.Empty;
    public long TotalRequests { get; set; }
    public long SuccessfulRequests { get; set; }
    public long FailedRequests { get; set; }
    public double AverageLatencyMs { get; set; }
    public double P95LatencyMs { get; set; }
    public double P99LatencyMs { get; set; }
    public Dictionary<int, long> StatusCodes { get; set; } = new();
}
