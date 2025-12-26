namespace USP.Core.Models.DTOs.Workspace;

/// <summary>
/// Cost allocation report for a workspace
/// </summary>
public class WorkspaceCostAllocationDto
{
    public Guid WorkspaceId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    // Subscription costs
    public long BaseSubscriptionCostCents { get; set; }
    public string SubscriptionTier { get; set; } = "free";

    // Usage-based costs
    public long ComputeCostCents { get; set; }
    public long StorageCostCents { get; set; }
    public long BandwidthCostCents { get; set; }
    public long ApiRequestCostCents { get; set; }

    // Feature costs
    public long SessionRecordingCostCents { get; set; }
    public long AdvancedComplianceCostCents { get; set; }
    public long SupportCostCents { get; set; }

    // Total
    public long TotalCostCents { get; set; }

    public string TotalCostFormatted => $"${TotalCostCents / 100.0:F2}";
}
