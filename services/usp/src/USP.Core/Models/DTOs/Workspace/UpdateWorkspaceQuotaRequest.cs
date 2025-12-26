using System.ComponentModel.DataAnnotations;

namespace USP.Core.Models.DTOs.Workspace;

/// <summary>
/// Request to update workspace quota (admin only)
/// </summary>
public class UpdateWorkspaceQuotaRequest
{
    [Range(1, 10000)]
    public int? MaxUsers { get; set; }

    [Range(1, 1000000)]
    public int? MaxSecrets { get; set; }

    [Range(1, 10000)]
    public int? MaxPrivilegedAccounts { get; set; }

    [Range(1, 1000)]
    public int? MaxPamSessions { get; set; }

    [Range(100, 10000000)]
    public int? MaxApiRequestsPerHour { get; set; }

    [Range(100, 1000000)]
    public long? MaxStorageMb { get; set; }

    public bool? SessionRecordingEnabled { get; set; }

    public bool? AdvancedComplianceEnabled { get; set; }

    public bool? CustomAuthMethodsEnabled { get; set; }

    [Range(7, 3650)]
    public int? AuditRetentionDays { get; set; }
}
