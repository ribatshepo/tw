namespace USP.Core.Models.Entities;

/// <summary>
/// Represents a user's membership in a workspace with role assignment
/// </summary>
public class WorkspaceMember
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public Guid UserId { get; set; }

    /// <summary>
    /// Role within this workspace: owner, admin, member, viewer
    /// </summary>
    public string Role { get; set; } = "member";

    /// <summary>
    /// Whether this member is active in the workspace
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Invitation status: invited, accepted, declined
    /// </summary>
    public string InvitationStatus { get; set; } = "accepted";

    /// <summary>
    /// Invitation token for pending invitations
    /// </summary>
    public string? InvitationToken { get; set; }

    /// <summary>
    /// When the invitation expires
    /// </summary>
    public DateTime? InvitationExpiresAt { get; set; }

    /// <summary>
    /// Who invited this member
    /// </summary>
    public Guid? InvitedBy { get; set; }

    /// <summary>
    /// When the member was invited
    /// </summary>
    public DateTime InvitedAt { get; set; } = DateTime.UtcNow;

    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LeftAt { get; set; }
    public DateTime? LastAccessedAt { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual Workspace Workspace { get; set; } = null!;
    public virtual ApplicationUser User { get; set; } = null!;
    public virtual ApplicationUser? Inviter { get; set; }
}
