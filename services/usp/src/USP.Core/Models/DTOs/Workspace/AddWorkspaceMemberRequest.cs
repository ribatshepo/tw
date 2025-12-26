using System.ComponentModel.DataAnnotations;

namespace USP.Core.Models.DTOs.Workspace;

/// <summary>
/// Request to add a member to a workspace
/// </summary>
public class AddWorkspaceMemberRequest
{
    /// <summary>
    /// User ID or email address
    /// </summary>
    [Required]
    public string UserIdentifier { get; set; } = string.Empty;

    /// <summary>
    /// User ID (when adding by ID)
    /// </summary>
    public Guid? UserId { get; set; }

    /// <summary>
    /// Role for the member: owner, admin, member, viewer
    /// </summary>
    [Required]
    public string Role { get; set; } = "member";

    /// <summary>
    /// Send email invitation if user doesn't exist
    /// </summary>
    public bool SendInvitation { get; set; } = true;
}
