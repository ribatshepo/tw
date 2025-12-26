namespace USP.Core.Models.DTOs.Workspace;

/// <summary>
/// Workspace compliance posture
/// </summary>
public class WorkspaceComplianceDto
{
    public Guid WorkspaceId { get; set; }
    public string WorkspaceName { get; set; } = string.Empty;

    // Compliance frameworks
    public bool Soc2Compliant { get; set; }
    public bool HipaaCompliant { get; set; }
    public bool PciDssCompliant { get; set; }
    public bool GdprCompliant { get; set; }

    // Control counts
    public int TotalControls { get; set; }
    public int ImplementedControls { get; set; }
    public int PartialControls { get; set; }
    public int NotImplementedControls { get; set; }

    // Compliance score (0-100)
    public int ComplianceScore { get; set; }

    // Issues
    public List<string> ComplianceGaps { get; set; } = new();
    public List<string> RequiredActions { get; set; } = new();

    public DateTime LastAssessment { get; set; }
    public DateTime? NextAssessment { get; set; }
}
