using System.ComponentModel.DataAnnotations;

namespace USP.Core.Models.DTOs.Workspace;

/// <summary>
/// Request to update workspace settings
/// </summary>
public class UpdateWorkspaceRequest
{
    /// <summary>
    /// Workspace display name
    /// </summary>
    [StringLength(100, MinimumLength = 2)]
    public string? Name { get; set; }

    /// <summary>
    /// Workspace description
    /// </summary>
    [StringLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// Custom domain for the workspace
    /// </summary>
    [StringLength(100)]
    public string? CustomDomain { get; set; }

    /// <summary>
    /// Require MFA for all workspace members
    /// </summary>
    public bool? RequireMfa { get; set; }

    /// <summary>
    /// Minimum password length
    /// </summary>
    [Range(8, 128)]
    public int? MinPasswordLength { get; set; }

    /// <summary>
    /// Session timeout in minutes
    /// </summary>
    [Range(5, 10080)]
    public int? SessionTimeoutMinutes { get; set; }

    /// <summary>
    /// IP whitelist (comma-separated)
    /// </summary>
    public string? IpWhitelist { get; set; }

    /// <summary>
    /// Workspace settings JSON
    /// </summary>
    public string? Settings { get; set; }

    /// <summary>
    /// Metadata JSON
    /// </summary>
    public string? Metadata { get; set; }
}
