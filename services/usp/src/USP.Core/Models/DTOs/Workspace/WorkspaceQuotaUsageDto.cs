namespace USP.Core.Models.DTOs.Workspace;

/// <summary>
/// Combined quota and usage information
/// </summary>
public class WorkspaceQuotaUsageDto
{
    public Guid WorkspaceId { get; set; }
    public string WorkspaceName { get; set; } = string.Empty;
    public string SubscriptionTier { get; set; } = "free";

    // Quotas
    public int MaxUsers { get; set; }
    public int MaxSecrets { get; set; }
    public int MaxPrivilegedAccounts { get; set; }
    public int MaxPamSessions { get; set; }
    public int MaxApiRequestsPerHour { get; set; }
    public long MaxStorageMb { get; set; }

    // Current Usage
    public int CurrentUsers { get; set; }
    public int CurrentSecrets { get; set; }
    public int CurrentPrivilegedAccounts { get; set; }
    public int CurrentPamSessions { get; set; }
    public int ApiRequestsThisHour { get; set; }
    public long CurrentStorageMb { get; set; }

    // Utilization percentages
    public double UserUtilization => MaxUsers > 0 ? (double)CurrentUsers / MaxUsers * 100 : 0;
    public double SecretUtilization => MaxSecrets > 0 ? (double)CurrentSecrets / MaxSecrets * 100 : 0;
    public double StorageUtilization => MaxStorageMb > 0 ? (double)CurrentStorageMb / MaxStorageMb * 100 : 0;

    // Warnings
    public List<string> QuotaWarnings { get; set; } = new();
}
