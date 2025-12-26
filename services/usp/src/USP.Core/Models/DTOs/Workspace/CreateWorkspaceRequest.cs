using System.ComponentModel.DataAnnotations;

namespace USP.Core.Models.DTOs.Workspace;

/// <summary>
/// Request to create a new workspace
/// </summary>
public class CreateWorkspaceRequest
{
    /// <summary>
    /// Workspace display name
    /// </summary>
    [Required]
    [StringLength(100, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Unique slug for the workspace (used in URLs)
    /// </summary>
    [Required]
    [StringLength(50, MinimumLength = 2)]
    [RegularExpression("^[a-z0-9-]+$", ErrorMessage = "Slug must contain only lowercase letters, numbers, and hyphens")]
    public string Slug { get; set; } = string.Empty;

    /// <summary>
    /// Optional description
    /// </summary>
    [StringLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// Subscription tier: free, pro, enterprise
    /// </summary>
    [Required]
    public string SubscriptionTier { get; set; } = "free";

    /// <summary>
    /// Parent workspace ID (optional, for hierarchical workspaces)
    /// </summary>
    public Guid? ParentWorkspaceId { get; set; }
}
