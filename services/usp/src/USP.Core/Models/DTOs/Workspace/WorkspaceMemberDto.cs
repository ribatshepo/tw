namespace USP.Core.Models.DTOs.Workspace;

/// <summary>
/// Workspace member data transfer object
/// </summary>
public class WorkspaceMemberDto
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public Guid UserId { get; set; }
    public string UserEmail { get; set; } = string.Empty;
    public string? UserFullName { get; set; }
    public string Role { get; set; } = "member";
    public bool IsActive { get; set; }
    public string InvitationStatus { get; set; } = "accepted";
    public DateTime JoinedAt { get; set; }
    public DateTime? LastAccessedAt { get; set; }
}
