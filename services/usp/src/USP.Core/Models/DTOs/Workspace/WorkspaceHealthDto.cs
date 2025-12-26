namespace USP.Core.Models.DTOs.Workspace;

/// <summary>
/// Workspace health score and metrics
/// </summary>
public class WorkspaceHealthDto
{
    public Guid WorkspaceId { get; set; }
    public string WorkspaceName { get; set; } = string.Empty;

    /// <summary>
    /// Overall health score (0-100)
    /// </summary>
    public int HealthScore { get; set; }

    /// <summary>
    /// Health status: healthy, warning, critical
    /// </summary>
    public string HealthStatus { get; set; } = "healthy";

    // Component scores (0-100 each)
    public int SecurityScore { get; set; }
    public int ComplianceScore { get; set; }
    public int AvailabilityScore { get; set; }
    public int PerformanceScore { get; set; }

    // Issues
    public List<string> SecurityIssues { get; set; } = new();
    public List<string> ComplianceIssues { get; set; } = new();
    public List<string> PerformanceIssues { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();

    public DateTime CalculatedAt { get; set; }
}
