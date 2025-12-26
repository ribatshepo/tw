using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Diagnostics;

namespace USP.Api.Observability;

/// <summary>
/// Configures OpenTelemetry distributed tracing for USP.
/// Provides automatic instrumentation for ASP.NET Core, Entity Framework Core, Redis, and gRPC.
/// Exports traces to Jaeger for visualization and analysis.
/// </summary>
public static class TracingConfiguration
{
    /// <summary>
    /// Activity source for custom USP spans.
    /// Use this to create custom spans for business operations.
    /// </summary>
    public static readonly ActivitySource ActivitySource = new("USP.Api", "1.0.0");

    /// <summary>
    /// Adds OpenTelemetry tracing to the service collection.
    /// </summary>
    public static IServiceCollection AddOpenTelemetryTracing(this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment environment)
    {
        var jaegerSettings = configuration.GetSection("Jaeger");
        var jaegerHost = jaegerSettings.GetValue<string>("Host") ?? "localhost";
        var jaegerPort = jaegerSettings.GetValue<int>("Port", 6831);
        var serviceName = jaegerSettings.GetValue<string>("ServiceName") ?? "usp-api";
        var serviceVersion = jaegerSettings.GetValue<string>("ServiceVersion") ?? "1.0.0";

        // Determine sampling ratio based on environment
        var samplingRatio = environment.IsProduction()
            ? jaegerSettings.GetValue<double>("SamplingRatio", 0.1) // 10% in production
            : 1.0; // 100% in development

        services.AddOpenTelemetry()
            .WithTracing(builder =>
            {
                builder
                    // Configure resource attributes
                    .SetResourceBuilder(ResourceBuilder.CreateDefault()
                        .AddService(
                            serviceName: serviceName,
                            serviceVersion: serviceVersion,
                            serviceInstanceId: Environment.MachineName)
                        .AddAttributes(new Dictionary<string, object>
                        {
                            ["environment"] = environment.EnvironmentName,
                            ["host.name"] = Environment.MachineName,
                            ["deployment.environment"] = environment.EnvironmentName
                        }))

                    // Add instrumentation sources
                    .AddSource(ActivitySource.Name)

                    // ASP.NET Core instrumentation (HTTP requests, middleware)
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        options.RecordException = true;
                        options.EnrichWithHttpRequest = (activity, request) =>
                        {
                            activity.SetTag("http.client_ip", request.HttpContext.Connection.RemoteIpAddress?.ToString());
                            activity.SetTag("http.user_agent", request.Headers.UserAgent.ToString());
                            activity.SetTag("http.request_content_length", request.ContentLength);
                        };
                        options.EnrichWithHttpResponse = (activity, response) =>
                        {
                            activity.SetTag("http.response_content_length", response.ContentLength);
                        };
                        // Filter out health check and metrics endpoints
                        options.Filter = (context) =>
                        {
                            var path = context.Request.Path.Value ?? string.Empty;
                            return !path.StartsWith("/health") &&
                                   !path.StartsWith("/metrics") &&
                                   !path.StartsWith("/_framework");
                        };
                    })

                    // HTTP client instrumentation (outbound HTTP calls)
                    .AddHttpClientInstrumentation(options =>
                    {
                        options.RecordException = true;
                        options.EnrichWithHttpRequestMessage = (activity, request) =>
                        {
                            activity.SetTag("http.request.method", request.Method.ToString());
                            activity.SetTag("http.request.url", request.RequestUri?.ToString());
                        };
                        options.EnrichWithHttpResponseMessage = (activity, response) =>
                        {
                            activity.SetTag("http.response.status_code", (int)response.StatusCode);
                        };
                    })

                    // Sampling strategy
                    .SetSampler(new TraceIdRatioBasedSampler(samplingRatio))

                    // Jaeger exporter
                    .AddJaegerExporter(options =>
                    {
                        options.AgentHost = jaegerHost;
                        options.AgentPort = jaegerPort;
                        options.MaxPayloadSizeInBytes = 4096;
                        options.ExportProcessorType = ExportProcessorType.Batch;
                        options.BatchExportProcessorOptions = new()
                        {
                            MaxQueueSize = 2048,
                            ScheduledDelayMilliseconds = 5000,
                            ExporterTimeoutMilliseconds = 30000,
                            MaxExportBatchSize = 512
                            };
                        });
    
                    // Console exporter for development (optional)
                    if (environment.IsDevelopment())
                    {
                        builder.AddConsoleExporter(options =>
                        {
                            options.Targets = OpenTelemetry.Exporter.ConsoleExporterOutputTargets.Debug;
                        });
                    }
            });

        return services;
    }

    /// <summary>
    /// Creates a new activity (span) for a business operation.
    /// </summary>
    /// <param name="operationName">Name of the operation (e.g., "AuthenticateUser", "RotatePassword")</param>
    /// <param name="kind">Activity kind (internal, server, client, producer, consumer)</param>
    /// <returns>Activity instance or null if not sampled</returns>
    public static Activity? StartActivity(string operationName, ActivityKind kind = ActivityKind.Internal)
    {
        return ActivitySource.StartActivity(operationName, kind);
    }

    /// <summary>
    /// Enriches the current activity with authentication context.
    /// </summary>
    public static void EnrichWithAuthenticationContext(Activity? activity, string userId, string userName, string method)
    {
        if (activity == null) return;

        activity.SetTag("auth.user_id", userId);
        activity.SetTag("auth.user_name", userName);
        activity.SetTag("auth.method", method);
    }

    /// <summary>
    /// Enriches the current activity with database operation context.
    /// </summary>
    public static void EnrichWithDatabaseContext(Activity? activity, string operation, string table, int? rowCount = null)
    {
        if (activity == null) return;

        activity.SetTag("db.operation", operation);
        activity.SetTag("db.table", table);
        if (rowCount.HasValue)
        {
            activity.SetTag("db.row_count", rowCount.Value);
        }
    }

    /// <summary>
    /// Enriches the current activity with secrets engine context.
    /// </summary>
    public static void EnrichWithSecretsContext(Activity? activity, string engine, string operation, string path)
    {
        if (activity == null) return;

        activity.SetTag("secrets.engine", engine);
        activity.SetTag("secrets.operation", operation);
        activity.SetTag("secrets.path", path);
    }

    /// <summary>
    /// Enriches the current activity with PAM context.
    /// </summary>
    public static void EnrichWithPamContext(Activity? activity, string safe, string account, string operation)
    {
        if (activity == null) return;

        activity.SetTag("pam.safe", safe);
        activity.SetTag("pam.account", account);
        activity.SetTag("pam.operation", operation);
    }

    /// <summary>
    /// Records an exception in the current activity.
    /// </summary>
    public static void RecordException(Activity? activity, Exception exception)
    {
        if (activity == null) return;

        activity.RecordException(exception);
        activity.SetStatus(ActivityStatusCode.Error, exception.Message);
    }

    /// <summary>
    /// Sets the activity status to OK.
    /// </summary>
    public static void SetOk(Activity? activity)
    {
        activity?.SetStatus(ActivityStatusCode.Ok);
    }

    /// <summary>
    /// Sets the activity status to error with a description.
    /// </summary>
    public static void SetError(Activity? activity, string description)
    {
        if (activity == null) return;

        activity.SetStatus(ActivityStatusCode.Error, description);
    }

    /// <summary>
    /// Adds a custom tag to the current activity.
    /// </summary>
    public static void AddTag(Activity? activity, string key, object? value)
    {
        if (activity == null || value == null) return;

        activity.SetTag(key, value);
    }

    /// <summary>
    /// Adds an event to the current activity.
    /// </summary>
    public static void AddEvent(Activity? activity, string name, Dictionary<string, object?>? tags = null)
    {
        if (activity == null) return;

        if (tags != null)
        {
            var tagList = new ActivityTagsCollection();
            foreach (var tag in tags)
            {
                if (tag.Value != null)
                {
                    tagList.Add(tag.Key, tag.Value);
                }
            }
            activity.AddEvent(new ActivityEvent(name, tags: tagList));
        }
        else
        {
            activity.AddEvent(new ActivityEvent(name));
        }
    }
}
