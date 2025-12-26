using USP.Core.Models.DTOs.Workspace;
using USP.Core.Models.Entities;

namespace USP.Core.Services.Workspace;

/// <summary>
/// Service for managing workspaces and multi-tenancy
/// </summary>
public interface IWorkspaceService
{
    /// <summary>
    /// Create a new workspace
    /// </summary>
    Task<WorkspaceDto> CreateWorkspaceAsync(CreateWorkspaceRequest request, Guid ownerId);

    /// <summary>
    /// Get workspace by ID
    /// </summary>
    Task<WorkspaceDto?> GetWorkspaceAsync(Guid workspaceId);

    /// <summary>
    /// Get workspace by slug
    /// </summary>
    Task<WorkspaceDto?> GetWorkspaceBySlugAsync(string slug);

    /// <summary>
    /// Get workspace by custom domain
    /// </summary>
    Task<WorkspaceDto?> GetWorkspaceByDomainAsync(string domain);

    /// <summary>
    /// Update workspace settings
    /// </summary>
    Task<WorkspaceDto> UpdateWorkspaceAsync(Guid workspaceId, UpdateWorkspaceRequest request);

    /// <summary>
    /// Delete (soft delete) a workspace
    /// </summary>
    Task DeleteWorkspaceAsync(Guid workspaceId);

    /// <summary>
    /// Get workspaces for a user
    /// </summary>
    Task<List<WorkspaceDto>> GetUserWorkspacesAsync(Guid userId);

    /// <summary>
    /// Add member to workspace
    /// </summary>
    Task<WorkspaceMemberDto> AddMemberAsync(Guid workspaceId, AddWorkspaceMemberRequest request, Guid invitedBy);

    /// <summary>
    /// Remove member from workspace
    /// </summary>
    Task RemoveMemberAsync(Guid workspaceId, Guid userId);

    /// <summary>
    /// Update member role
    /// </summary>
    Task<WorkspaceMemberDto> UpdateMemberRoleAsync(Guid workspaceId, Guid userId, string role);

    /// <summary>
    /// Get workspace members
    /// </summary>
    Task<List<WorkspaceMemberDto>> GetMembersAsync(Guid workspaceId);

    /// <summary>
    /// Check if user has access to workspace
    /// </summary>
    Task<bool> HasAccessAsync(Guid userId, Guid workspaceId);

    /// <summary>
    /// Get user's role in workspace
    /// </summary>
    Task<string?> GetUserRoleAsync(Guid userId, Guid workspaceId);

    /// <summary>
    /// Check quota availability
    /// </summary>
    Task<bool> CheckQuotaAsync(Guid workspaceId, string quotaType);

    /// <summary>
    /// Increment usage counter
    /// </summary>
    Task IncrementUsageAsync(Guid workspaceId, string usageType);

    /// <summary>
    /// Decrement usage counter
    /// </summary>
    Task DecrementUsageAsync(Guid workspaceId, string usageType);

    /// <summary>
    /// Get workspace quota and usage statistics
    /// </summary>
    Task<WorkspaceQuotaUsageDto> GetQuotaUsageAsync(Guid workspaceId);

    /// <summary>
    /// Update workspace quota (admin only)
    /// </summary>
    Task UpdateQuotaAsync(Guid workspaceId, UpdateWorkspaceQuotaRequest request);

    /// <summary>
    /// Accept workspace invitation
    /// </summary>
    Task AcceptInvitationAsync(string invitationToken, Guid userId);

    /// <summary>
    /// Suspend workspace (admin only)
    /// </summary>
    Task SuspendWorkspaceAsync(Guid workspaceId, string reason);

    /// <summary>
    /// Activate workspace (admin only)
    /// </summary>
    Task ActivateWorkspaceAsync(Guid workspaceId);
}
