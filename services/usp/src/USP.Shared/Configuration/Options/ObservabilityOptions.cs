namespace USP.Shared.Configuration.Options;

public class ObservabilityOptions
{
    public string ElasticsearchUri { get; set; } = "http://elasticsearch:9200";
    public string JaegerAgentHost { get; set; } = "jaeger";
    public int JaegerAgentPort { get; set; } = 6831;
    public string PrometheusEndpoint { get; set; } = "/metrics";
    public string LogIndexFormat { get; set; } = "usp-logs-{0:yyyy.MM.dd}";
}
