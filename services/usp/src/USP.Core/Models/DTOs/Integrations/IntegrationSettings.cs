namespace USP.Core.Models.DTOs.Integrations;

/// <summary>
/// Splunk integration settings
/// </summary>
public class SplunkSettings
{
    public string HecUrl { get; set; } = string.Empty;
    public string HecToken { get; set; } = string.Empty;
    public string Index { get; set; } = "usp_audit";
    public string SourceType { get; set; } = "usp:audit";
    public string Source { get; set; } = "usp";
    public bool VerifySsl { get; set; } = true;
    public bool Enabled { get; set; } = false;
}

/// <summary>
/// Elasticsearch integration settings
/// </summary>
public class ElasticsearchSettings
{
    public string Url { get; set; } = string.Empty;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string IndexPrefix { get; set; } = "usp-audit";
    public bool Enabled { get; set; } = false;
}

/// <summary>
/// Datadog integration settings
/// </summary>
public class DatadogSettings
{
    public string ApiKey { get; set; } = string.Empty;
    public string ApplicationKey { get; set; } = string.Empty;
    public string Site { get; set; } = "datadoghq.com";
    public bool Enabled { get; set; } = false;
}

/// <summary>
/// Slack integration settings
/// </summary>
public class SlackSettings
{
    public string WebhookUrl { get; set; } = string.Empty;
    public string Channel { get; set; } = "#security-alerts";
    public string BotName { get; set; } = "USP Security Bot";
    public bool Enabled { get; set; } = false;
}

/// <summary>
/// Microsoft Teams integration settings
/// </summary>
public class TeamsSettings
{
    public string WebhookUrl { get; set; } = string.Empty;
    public bool Enabled { get; set; } = false;
}

/// <summary>
/// PagerDuty integration settings
/// </summary>
public class PagerDutySettings
{
    public string IntegrationKey { get; set; } = string.Empty;
    public string ApiUrl { get; set; } = "https://events.pagerduty.com/v2/enqueue";
    public bool Enabled { get; set; } = false;
}

/// <summary>
/// Jira integration settings
/// </summary>
public class JiraSettings
{
    public string BaseUrl { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string ApiToken { get; set; } = string.Empty;
    public string ProjectKey { get; set; } = string.Empty;
    public string IssueType { get; set; } = "Security Incident";
    public bool Enabled { get; set; } = false;
}

/// <summary>
/// ServiceNow integration settings
/// </summary>
public class ServiceNowSettings
{
    public string InstanceUrl { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string AssignmentGroup { get; set; } = "Security";
    public bool Enabled { get; set; } = false;
}

/// <summary>
/// GitHub Actions integration settings
/// </summary>
public class GitHubSettings
{
    public string PersonalAccessToken { get; set; } = string.Empty;
    public string AppId { get; set; } = string.Empty;
    public string AppPrivateKey { get; set; } = string.Empty;
    public bool Enabled { get; set; } = false;
}

/// <summary>
/// GitLab CI integration settings
/// </summary>
public class GitLabSettings
{
    public string BaseUrl { get; set; } = "https://gitlab.com";
    public string PersonalAccessToken { get; set; } = string.Empty;
    public bool Enabled { get; set; } = false;
}

/// <summary>
/// Jenkins integration settings
/// </summary>
public class JenkinsSettings
{
    public string BaseUrl { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string ApiToken { get; set; } = string.Empty;
    public bool Enabled { get; set; } = false;
}

/// <summary>
/// Kafka integration settings
/// </summary>
public class KafkaSettings
{
    public string BootstrapServers { get; set; } = string.Empty;
    public string AuditTopic { get; set; } = "usp.audit";
    public string ThreatTopic { get; set; } = "usp.threat";
    public string ComplianceTopic { get; set; } = "usp.compliance";
    public string SecurityGroup { get; set; } = "usp-security-group";
    public string? SaslUsername { get; set; }
    public string? SaslPassword { get; set; }
    public bool EnableSsl { get; set; } = true;
    public bool Enabled { get; set; } = false;
}
