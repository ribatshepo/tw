namespace USP.Core.Services.Integrations;

/// <summary>
/// Service mesh integration for mTLS certificate management and policy enforcement
/// </summary>
public interface IServiceMeshIntegration
{
    /// <summary>
    /// Issue mTLS certificate for service
    /// </summary>
    Task<ServiceCertificate> IssueCertificateAsync(string serviceId, CertificateRequest request);

    /// <summary>
    /// Revoke service certificate
    /// </summary>
    Task<bool> RevokeCertificateAsync(string certificateId);

    /// <summary>
    /// Rotate service certificate
    /// </summary>
    Task<ServiceCertificate> RotateCertificateAsync(string serviceId);

    /// <summary>
    /// Configure traffic policy
    /// </summary>
    Task<bool> ConfigureTrafficPolicyAsync(string serviceId, TrafficPolicy policy);

    /// <summary>
    /// Configure authorization policy
    /// </summary>
    Task<bool> ConfigureAuthorizationPolicyAsync(string serviceId, AuthorizationPolicy policy);

    /// <summary>
    /// Get service mesh health status
    /// </summary>
    Task<MeshHealthStatus> GetHealthStatusAsync();

    /// <summary>
    /// Get service telemetry data
    /// </summary>
    Task<ServiceTelemetry> GetTelemetryAsync(string serviceId, DateTime? startTime = null, DateTime? endTime = null);
}

public class ServiceCertificate
{
    public string CertificateId { get; set; } = string.Empty;
    public string ServiceId { get; set; } = string.Empty;
    public byte[] Certificate { get; set; } = Array.Empty<byte>();
    public byte[] PrivateKey { get; set; } = Array.Empty<byte>();
    public byte[] CertificateChain { get; set; } = Array.Empty<byte>();
    public DateTime IssuedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public string SerialNumber { get; set; } = string.Empty;
}

public class CertificateRequest
{
    public string CommonName { get; set; } = string.Empty;
    public List<string> SubjectAlternativeNames { get; set; } = new();
    public int ValidityDays { get; set; } = 90;
    public string KeyAlgorithm { get; set; } = "RSA2048"; // RSA2048, RSA4096, ECDSA256, ECDSA384
}

public class TrafficPolicy
{
    public string ServiceId { get; set; } = string.Empty;
    public ConnectionPool? ConnectionPool { get; set; }
    public LoadBalancer? LoadBalancer { get; set; }
    public OutlierDetection? OutlierDetection { get; set; }
    public Retry? Retry { get; set; }
}

public class ConnectionPool
{
    public int MaxConnections { get; set; } = 100;
    public int MaxPendingRequests { get; set; } = 100;
    public int MaxRequests { get; set; } = 1000;
    public int MaxRetries { get; set; } = 3;
}

public class LoadBalancer
{
    public string Strategy { get; set; } = "ROUND_ROBIN"; // ROUND_ROBIN, LEAST_REQUEST, RANDOM
}

public class OutlierDetection
{
    public int ConsecutiveErrors { get; set; } = 5;
    public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan BaseEjectionTime { get; set; } = TimeSpan.FromSeconds(30);
    public int MaxEjectionPercent { get; set; } = 50;
}

public class Retry
{
    public int Attempts { get; set; } = 3;
    public TimeSpan PerTryTimeout { get; set; } = TimeSpan.FromSeconds(10);
    public List<string> RetryOn { get; set; } = new() { "5xx", "gateway-error", "reset" };
}

public class AuthorizationPolicy
{
    public string ServiceId { get; set; } = string.Empty;
    public string Action { get; set; } = "ALLOW"; // ALLOW, DENY
    public List<PolicyRule> Rules { get; set; } = new();
}

public class PolicyRule
{
    public List<string> SourcePrincipals { get; set; } = new();
    public List<string> SourceNamespaces { get; set; } = new();
    public List<string> SourceIpBlocks { get; set; } = new();
    public List<string> Operations { get; set; } = new();
    public Dictionary<string, List<string>> When { get; set; } = new();
}

public class MeshHealthStatus
{
    public string Status { get; set; } = "unknown"; // healthy, degraded, unhealthy
    public int TotalServices { get; set; }
    public int HealthyServices { get; set; }
    public Dictionary<string, string> ComponentStatus { get; set; } = new();
    public DateTime LastCheck { get; set; }
}

public class ServiceTelemetry
{
    public string ServiceId { get; set; } = string.Empty;
    public long RequestCount { get; set; }
    public long ErrorCount { get; set; }
    public double ErrorRate { get; set; }
    public double AverageLatencyMs { get; set; }
    public double P50LatencyMs { get; set; }
    public double P95LatencyMs { get; set; }
    public double P99LatencyMs { get; set; }
    public Dictionary<string, long> InboundTraffic { get; set; } = new(); // source service -> request count
    public Dictionary<string, long> OutboundTraffic { get; set; } = new(); // destination service -> request count
}
