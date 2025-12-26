using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;
using StackExchange.Redis;
using System.Diagnostics;
using USP.Core.Services.Cryptography;

namespace USP.Api.Health;

/// <summary>
/// Comprehensive health check that validates all critical dependencies and system resources.
/// Checks: PostgreSQL, Redis, RabbitMQ, Elasticsearch, Jaeger, disk space, memory, CPU, and seal status.
/// </summary>
public class DetailedHealthCheck : IHealthCheck
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<DetailedHealthCheck> _logger;
    private readonly ISealManager? _sealManager;

    public DetailedHealthCheck(
        IConfiguration configuration,
        ILogger<DetailedHealthCheck> logger,
        ISealManager? sealManager = null)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _sealManager = sealManager;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var checks = new Dictionary<string, object>();
        var unhealthyChecks = new List<string>();
        var degradedChecks = new List<string>();

        // 1. PostgreSQL Database Check
        var dbHealth = await CheckPostgreSqlAsync(cancellationToken);
        checks["postgresql"] = dbHealth;
        if (dbHealth.Status == "unhealthy") unhealthyChecks.Add("postgresql");
        else if (dbHealth.Status == "degraded") degradedChecks.Add("postgresql");

        // 2. Redis Cache Check
        var redisHealth = await CheckRedisAsync(cancellationToken);
        checks["redis"] = redisHealth;
        if (redisHealth.Status == "unhealthy") unhealthyChecks.Add("redis");
        else if (redisHealth.Status == "degraded") degradedChecks.Add("redis");

        // 3. RabbitMQ Check
        var rabbitMqHealth = await CheckRabbitMqAsync(cancellationToken);
        checks["rabbitmq"] = rabbitMqHealth;
        if (rabbitMqHealth.Status == "unhealthy") unhealthyChecks.Add("rabbitmq");
        else if (rabbitMqHealth.Status == "degraded") degradedChecks.Add("rabbitmq");

        // 4. Elasticsearch Check
        var elasticsearchHealth = await CheckElasticsearchAsync(cancellationToken);
        checks["elasticsearch"] = elasticsearchHealth;
        if (elasticsearchHealth.Status == "unhealthy") unhealthyChecks.Add("elasticsearch");
        else if (elasticsearchHealth.Status == "degraded") degradedChecks.Add("elasticsearch");

        // 5. Jaeger Check
        var jaegerHealth = await CheckJaegerAsync(cancellationToken);
        checks["jaeger"] = jaegerHealth;
        if (jaegerHealth.Status == "unhealthy") unhealthyChecks.Add("jaeger");
        else if (jaegerHealth.Status == "degraded") degradedChecks.Add("jaeger");

        // 6. Disk Space Check
        var diskHealth = CheckDiskSpace();
        checks["disk_space"] = diskHealth;
        if (diskHealth.Status == "unhealthy") unhealthyChecks.Add("disk_space");
        else if (diskHealth.Status == "degraded") degradedChecks.Add("disk_space");

        // 7. Memory Usage Check
        var memoryHealth = CheckMemoryUsage();
        checks["memory"] = memoryHealth;
        if (memoryHealth.Status == "unhealthy") unhealthyChecks.Add("memory");
        else if (memoryHealth.Status == "degraded") degradedChecks.Add("memory");

        // 8. CPU Usage Check
        var cpuHealth = await CheckCpuUsageAsync(cancellationToken);
        checks["cpu"] = cpuHealth;
        if (cpuHealth.Status == "unhealthy") unhealthyChecks.Add("cpu");
        else if (cpuHealth.Status == "degraded") degradedChecks.Add("cpu");

        // 9. Seal Status Check (critical for USP)
        var sealHealth = CheckSealStatus();
        checks["seal_status"] = sealHealth;
        if (sealHealth.Status == "unhealthy") unhealthyChecks.Add("seal_status");
        else if (sealHealth.Status == "degraded") degradedChecks.Add("seal_status");

        // 10. Configuration Check
        var configHealth = CheckConfiguration();
        checks["configuration"] = configHealth;
        if (configHealth.Status == "unhealthy") unhealthyChecks.Add("configuration");
        else if (configHealth.Status == "degraded") degradedChecks.Add("configuration");

        // Determine overall health status
        if (unhealthyChecks.Any())
        {
            var message = $"Unhealthy components: {string.Join(", ", unhealthyChecks)}";
            _logger.LogError("Health check failed: {Message}", message);
            return HealthCheckResult.Unhealthy(message, data: checks);
        }

        if (degradedChecks.Any())
        {
            var message = $"Degraded components: {string.Join(", ", degradedChecks)}";
            _logger.LogWarning("Health check degraded: {Message}", message);
            return HealthCheckResult.Degraded(message, data: checks);
        }

        _logger.LogDebug("Health check passed - all components healthy");
        return HealthCheckResult.Healthy("All systems operational", data: checks);
    }

    private async Task<HealthCheckInfo> CheckPostgreSqlAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var connectionString = _configuration.GetSection("Database:ConnectionString").Value
                ?? BuildConnectionString();

            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = new NpgsqlCommand("SELECT 1", connection);
            var result = await command.ExecuteScalarAsync(cancellationToken);

            stopwatch.Stop();

            if (result?.ToString() == "1")
            {
                var latency = stopwatch.ElapsedMilliseconds;
                return new HealthCheckInfo
                {
                    Status = latency > 1000 ? "degraded" : "healthy",
                    LatencyMs = latency,
                    Message = $"Connected (latency: {latency}ms)"
                };
            }

            return new HealthCheckInfo { Status = "unhealthy", Message = "Query failed" };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "PostgreSQL health check failed");
            return new HealthCheckInfo
            {
                Status = "unhealthy",
                LatencyMs = stopwatch.ElapsedMilliseconds,
                Message = $"Connection failed: {ex.Message}"
            };
        }
    }

    private async Task<HealthCheckInfo> CheckRedisAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var redisHost = _configuration.GetValue<string>("Redis:Host") ?? "localhost";
            var redisPort = _configuration.GetValue<int>("Redis:Port", 6379);
            var redisPassword = _configuration.GetValue<string>("Redis:Password");

            var configOptions = new ConfigurationOptions
            {
                EndPoints = { { redisHost, redisPort } },
                AbortOnConnectFail = false,
                ConnectTimeout = 5000,
                SyncTimeout = 5000
            };

            if (!string.IsNullOrEmpty(redisPassword))
            {
                configOptions.Password = redisPassword;
            }

            var connection = await ConnectionMultiplexer.ConnectAsync(configOptions);
            var db = connection.GetDatabase();
            await db.PingAsync();

            stopwatch.Stop();
            var latency = stopwatch.ElapsedMilliseconds;

            return new HealthCheckInfo
            {
                Status = latency > 500 ? "degraded" : "healthy",
                LatencyMs = latency,
                Message = $"Connected (latency: {latency}ms)"
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Redis health check failed");
            return new HealthCheckInfo
            {
                Status = "unhealthy",
                LatencyMs = stopwatch.ElapsedMilliseconds,
                Message = $"Connection failed: {ex.Message}"
            };
        }
    }

    private async Task<HealthCheckInfo> CheckRabbitMqAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var rabbitHost = _configuration.GetValue<string>("RabbitMQ:Host") ?? "localhost";
            var rabbitPort = _configuration.GetValue<int>("RabbitMQ:Port", 5672);
            var rabbitUser = _configuration.GetValue<string>("RabbitMQ:Username") ?? "guest";
            var rabbitPassword = _configuration.GetValue<string>("RabbitMQ:Password") ?? "guest";

            var factory = new RabbitMQ.Client.ConnectionFactory
            {
                HostName = rabbitHost,
                Port = rabbitPort,
                UserName = rabbitUser,
                Password = rabbitPassword,
                RequestedConnectionTimeout = TimeSpan.FromSeconds(5),
                AutomaticRecoveryEnabled = false
            };

            using var connection = factory.CreateConnection();
            using var channel = connection.CreateModel();

            stopwatch.Stop();
            var latency = stopwatch.ElapsedMilliseconds;

            return new HealthCheckInfo
            {
                Status = latency > 1000 ? "degraded" : "healthy",
                LatencyMs = latency,
                Message = $"Connected (latency: {latency}ms)"
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "RabbitMQ health check failed");
            return new HealthCheckInfo
            {
                Status = "unhealthy",
                LatencyMs = stopwatch.ElapsedMilliseconds,
                Message = $"Connection failed: {ex.Message}"
            };
        }
    }

    private async Task<HealthCheckInfo> CheckElasticsearchAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var elasticsearchUrl = _configuration.GetValue<string>("Serilog:WriteTo:1:Args:nodeUris") ?? "http://localhost:9200";

            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var response = await client.GetAsync(elasticsearchUrl, cancellationToken);

            stopwatch.Stop();
            var latency = stopwatch.ElapsedMilliseconds;

            if (response.IsSuccessStatusCode)
            {
                return new HealthCheckInfo
                {
                    Status = latency > 1000 ? "degraded" : "healthy",
                    LatencyMs = latency,
                    Message = $"Connected (latency: {latency}ms)"
                };
            }

            return new HealthCheckInfo
            {
                Status = "unhealthy",
                LatencyMs = latency,
                Message = $"HTTP {(int)response.StatusCode}"
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Elasticsearch health check failed");
            return new HealthCheckInfo
            {
                Status = "degraded", // Not critical, just logging
                LatencyMs = stopwatch.ElapsedMilliseconds,
                Message = $"Connection failed: {ex.Message}"
            };
        }
    }

    private async Task<HealthCheckInfo> CheckJaegerAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var jaegerHost = _configuration.GetValue<string>("Jaeger:Host") ?? "localhost";
            var jaegerHttpPort = _configuration.GetValue<int>("Jaeger:HttpPort", 14268);

            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var response = await client.GetAsync($"http://{jaegerHost}:{jaegerHttpPort}/", cancellationToken);

            stopwatch.Stop();
            var latency = stopwatch.ElapsedMilliseconds;

            return new HealthCheckInfo
            {
                Status = response.IsSuccessStatusCode ? "healthy" : "degraded",
                LatencyMs = latency,
                Message = response.IsSuccessStatusCode
                    ? $"Connected (latency: {latency}ms)"
                    : $"HTTP {(int)response.StatusCode}"
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Jaeger health check failed");
            return new HealthCheckInfo
            {
                Status = "degraded", // Not critical, just tracing
                LatencyMs = stopwatch.ElapsedMilliseconds,
                Message = $"Connection failed: {ex.Message}"
            };
        }
    }

    private HealthCheckInfo CheckDiskSpace()
    {
        try
        {
            var drive = DriveInfo.GetDrives().FirstOrDefault(d => d.IsReady && d.Name == "/");
            if (drive == null)
            {
                drive = DriveInfo.GetDrives().FirstOrDefault(d => d.IsReady);
            }

            if (drive != null)
            {
                var totalSpaceGB = drive.TotalSize / (1024.0 * 1024.0 * 1024.0);
                var freeSpaceGB = drive.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);
                var usedPercentage = ((totalSpaceGB - freeSpaceGB) / totalSpaceGB) * 100;

                string status;
                if (usedPercentage > 95) status = "unhealthy";
                else if (usedPercentage > 85) status = "degraded";
                else status = "healthy";

                return new HealthCheckInfo
                {
                    Status = status,
                    Message = $"{freeSpaceGB:F2} GB free of {totalSpaceGB:F2} GB ({100 - usedPercentage:F1}% available)"
                };
            }

            return new HealthCheckInfo { Status = "healthy", Message = "No disk info available" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Disk space check failed");
            return new HealthCheckInfo { Status = "degraded", Message = $"Check failed: {ex.Message}" };
        }
    }

    private HealthCheckInfo CheckMemoryUsage()
    {
        try
        {
            var process = Process.GetCurrentProcess();
            var memoryMB = process.WorkingSet64 / (1024.0 * 1024.0);
            var gcMemoryMB = GC.GetTotalMemory(false) / (1024.0 * 1024.0);

            string status;
            if (memoryMB > 2048) status = "degraded"; // > 2GB
            else if (memoryMB > 4096) status = "unhealthy"; // > 4GB
            else status = "healthy";

            return new HealthCheckInfo
            {
                Status = status,
                Message = $"Process: {memoryMB:F2} MB, GC: {gcMemoryMB:F2} MB"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Memory check failed");
            return new HealthCheckInfo { Status = "degraded", Message = $"Check failed: {ex.Message}" };
        }
    }

    private async Task<HealthCheckInfo> CheckCpuUsageAsync(CancellationToken cancellationToken)
    {
        try
        {
            var process = Process.GetCurrentProcess();
            var startTime = DateTime.UtcNow;
            var startCpuUsage = process.TotalProcessorTime;

            await Task.Delay(500, cancellationToken);

            var endTime = DateTime.UtcNow;
            var endCpuUsage = process.TotalProcessorTime;

            var cpuUsedMs = (endCpuUsage - startCpuUsage).TotalMilliseconds;
            var totalMsPassed = (endTime - startTime).TotalMilliseconds;
            var cpuUsageTotal = cpuUsedMs / (Environment.ProcessorCount * totalMsPassed);
            var cpuPercentage = cpuUsageTotal * 100;

            string status;
            if (cpuPercentage > 90) status = "unhealthy";
            else if (cpuPercentage > 70) status = "degraded";
            else status = "healthy";

            return new HealthCheckInfo
            {
                Status = status,
                Message = $"{cpuPercentage:F1}% ({Environment.ProcessorCount} cores)"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CPU check failed");
            return new HealthCheckInfo { Status = "degraded", Message = $"Check failed: {ex.Message}" };
        }
    }

    private HealthCheckInfo CheckSealStatus()
    {
        try
        {
            if (_sealManager == null)
            {
                return new HealthCheckInfo
                {
                    Status = "healthy",
                    Message = "Seal manager not initialized (likely during startup)"
                };
            }

            var isSealed = _sealManager.IsSealed();

            if (isSealed)
            {
                return new HealthCheckInfo
                {
                    Status = "unhealthy",
                    Message = "USP is SEALED - unsealing required for normal operations"
                };
            }

            return new HealthCheckInfo
            {
                Status = "healthy",
                Message = "USP is unsealed and operational"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Seal status check failed");
            return new HealthCheckInfo
            {
                Status = "degraded",
                Message = $"Check failed: {ex.Message}"
            };
        }
    }

    private string BuildConnectionString()
    {
        var host = _configuration.GetValue<string>("Database:Host") ?? "localhost";
        var port = _configuration.GetValue<int>("Database:Port", 5432);
        var database = _configuration.GetValue<string>("Database:Database") ?? "unified_security_db";
        var username = _configuration.GetValue<string>("Database:Username") ?? "usp_user";
        var password = _configuration.GetValue<string>("Database:Password") ?? "";

        return $"Host={host};Port={port};Database={database};Username={username};Password={password}";
    }

    private HealthCheckInfo CheckConfiguration()
    {
        try
        {
            var issues = new List<string>();

            // Check critical configuration sections
            if (string.IsNullOrEmpty(_configuration["Jwt:Secret"]) &&
                string.IsNullOrEmpty(_configuration["Jwt:PrivateKeyPath"]))
            {
                issues.Add("JWT configuration missing");
            }

            if (string.IsNullOrEmpty(_configuration["Database:Host"]))
            {
                issues.Add("Database configuration missing");
            }

            if (string.IsNullOrEmpty(_configuration["WebAuthn:RelyingPartyId"]))
            {
                issues.Add("WebAuthn configuration missing");
            }

            if (issues.Any())
            {
                return new HealthCheckInfo
                {
                    Status = "degraded",
                    Message = $"Configuration issues: {string.Join(", ", issues)}"
                };
            }

            return new HealthCheckInfo
            {
                Status = "healthy",
                Message = "All critical configuration present"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Configuration health check failed");
            return new HealthCheckInfo
            {
                Status = "degraded",
                Message = $"Check failed: {ex.Message}"
            };
        }
    }

    private class HealthCheckInfo
    {
        public string Status { get; set; } = "unknown";
        public long? LatencyMs { get; set; }
        public string Message { get; set; } = "";
    }
}
