namespace USP.Core.Models.DTOs.Workspace;

/// <summary>
/// Resource consumption metrics for a workspace
/// </summary>
public class WorkspaceResourceConsumptionDto
{
    public Guid WorkspaceId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    // Compute resources
    public long TotalApiRequests { get; set; }
    public double AverageResponseTimeMs { get; set; }
    public long TotalCpuTimeMs { get; set; }

    // Storage resources
    public long StorageUsedMb { get; set; }
    public long SecretsStorageMb { get; set; }
    public long SessionRecordingsStorageMb { get; set; }
    public long AuditLogsStorageMb { get; set; }

    // Database resources
    public long TotalDatabaseQueries { get; set; }
    public double AverageQueryTimeMs { get; set; }

    // Network resources
    public long TotalBandwidthMb { get; set; }
    public long InboundBandwidthMb { get; set; }
    public long OutboundBandwidthMb { get; set; }
}
