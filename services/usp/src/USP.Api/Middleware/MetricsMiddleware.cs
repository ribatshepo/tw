using System.Diagnostics;
using USP.Api.Metrics;

namespace USP.Api.Middleware;

/// <summary>
/// Middleware that tracks detailed HTTP request metrics using Prometheus.
/// Captures request duration, status codes, payload sizes, and custom business metrics.
/// </summary>
public class MetricsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<MetricsMiddleware> _logger;

    // Additional metrics specific to HTTP requests
    private static readonly Prometheus.Histogram RequestDuration = Prometheus.Metrics.CreateHistogram(
        "usp_http_request_duration_seconds",
        "Duration of HTTP requests in seconds",
        new Prometheus.HistogramConfiguration
        {
            LabelNames = new[] { "method", "endpoint", "status_code" },
            Buckets = new[] { 0.001, 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1.0, 2.5, 5.0 }
        });

    private static readonly Prometheus.Counter RequestTotal = Prometheus.Metrics.CreateCounter(
        "usp_http_requests_total",
        "Total number of HTTP requests",
        new Prometheus.CounterConfiguration
        {
            LabelNames = new[] { "method", "endpoint", "status_code" }
        });

    private static readonly Prometheus.Histogram RequestSize = Prometheus.Metrics.CreateHistogram(
        "usp_http_request_size_bytes",
        "Size of HTTP request payload in bytes",
        new Prometheus.HistogramConfiguration
        {
            LabelNames = new[] { "method", "endpoint" },
            Buckets = new[] { 100, 1000, 10000, 100000, 1000000 }
        });

    private static readonly Prometheus.Histogram ResponseSize = Prometheus.Metrics.CreateHistogram(
        "usp_http_response_size_bytes",
        "Size of HTTP response payload in bytes",
        new Prometheus.HistogramConfiguration
        {
            LabelNames = new[] { "method", "endpoint", "status_code" },
            Buckets = new[] { 100, 1000, 10000, 100000, 1000000 }
        });

    private static readonly Prometheus.Gauge ActiveRequests = Prometheus.Metrics.CreateGauge(
        "usp_http_active_requests",
        "Number of currently active HTTP requests");

    public MetricsMiddleware(RequestDelegate next, ILogger<MetricsMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip metrics endpoint to avoid circular metrics
        if (context.Request.Path.StartsWithSegments("/metrics"))
        {
            await _next(context);
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        ActiveRequests.Inc();

        var method = context.Request.Method;
        var endpoint = GetEndpointName(context);
        var requestSize = context.Request.ContentLength ?? 0;

        // Track request size
        if (requestSize > 0)
        {
            RequestSize.WithLabels(method, endpoint).Observe(requestSize);
        }

        // Create a wrapper for the response body to track size
        var originalBodyStream = context.Response.Body;
        using var responseBodyStream = new MemoryStream();
        context.Response.Body = responseBodyStream;

        try
        {
            // Process the request
            await _next(context);

            stopwatch.Stop();
            var statusCode = context.Response.StatusCode.ToString();
            var duration = stopwatch.Elapsed.TotalSeconds;

            // Record metrics
            RequestDuration.WithLabels(method, endpoint, statusCode).Observe(duration);
            RequestTotal.WithLabels(method, endpoint, statusCode).Inc();

            // Track response size
            var responseSize = responseBodyStream.Length;
            if (responseSize > 0)
            {
                ResponseSize.WithLabels(method, endpoint, statusCode).Observe(responseSize);
            }

            // Copy response back to original stream
            responseBodyStream.Seek(0, SeekOrigin.Begin);
            await responseBodyStream.CopyToAsync(originalBodyStream);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            var statusCode = context.Response.StatusCode > 0 ? context.Response.StatusCode.ToString() : "500";
            var duration = stopwatch.Elapsed.TotalSeconds;

            // Record metrics even for failed requests
            RequestDuration.WithLabels(method, endpoint, statusCode).Observe(duration);
            RequestTotal.WithLabels(method, endpoint, statusCode).Inc();

            // Record error
            PrometheusMetrics.RecordError("http_request");

            _logger.LogError(ex, "Error processing request {Method} {Path}", method, context.Request.Path);

            // Re-throw to let error handling middleware handle it
            throw;
        }
        finally
        {
            ActiveRequests.Dec();
            context.Response.Body = originalBodyStream;
        }
    }

    /// <summary>
    /// Extracts a normalized endpoint name from the request path.
    /// Replaces dynamic segments with placeholders to reduce cardinality.
    /// </summary>
    private static string GetEndpointName(HttpContext context)
    {
        var endpoint = context.GetEndpoint();
        if (endpoint != null)
        {
            // Use route pattern if available
            var routePattern = endpoint.Metadata.GetMetadata<Microsoft.AspNetCore.Routing.RouteEndpoint>()?.RoutePattern;
            if (routePattern != null)
            {
                return routePattern.RawText ?? context.Request.Path.Value ?? "/";
            }
        }

        // Fallback: normalize the path to reduce cardinality
        var path = context.Request.Path.Value ?? "/";

        // Replace common ID patterns with placeholders
        path = System.Text.RegularExpressions.Regex.Replace(path, @"/\d+", "/{id}");
        path = System.Text.RegularExpressions.Regex.Replace(path, @"/[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}", "/{guid}");

        return path;
    }
}
