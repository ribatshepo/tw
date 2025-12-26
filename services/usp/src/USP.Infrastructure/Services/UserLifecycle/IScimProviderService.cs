using USP.Core.Models.DTOs.SCIM;
using USP.Core.Models.DTOs.UserLifecycle;

namespace USP.Infrastructure.Services.UserLifecycle;

/// <summary>
/// Interface for SCIM 2.0 provider service
/// </summary>
public interface IScimProviderService
{
    // User operations
    Task<ScimListResponse<ScimUserResource>> GetUsersAsync(string? filter, int startIndex, int count, string? attributes);
    Task<ScimUserResource?> GetUserByIdAsync(Guid userId, string? attributes);
    Task<ScimUserResource> CreateUserAsync(ScimUserResource user);
    Task<ScimUserResource> UpdateUserAsync(Guid userId, ScimUserResource user);
    Task<ScimUserResource> PatchUserAsync(Guid userId, ScimPatchRequest patchRequest);
    Task DeleteUserAsync(Guid userId);

    // Group operations
    Task<ScimListResponse<ScimGroupResource>> GetGroupsAsync(string? filter, int startIndex, int count, string? attributes);
    Task<ScimGroupResource?> GetGroupByIdAsync(Guid groupId, string? attributes);
    Task<ScimGroupResource> CreateGroupAsync(ScimGroupResource group);
    Task<ScimGroupResource> UpdateGroupAsync(Guid groupId, ScimGroupResource group);
    Task<ScimGroupResource> PatchGroupAsync(Guid groupId, ScimPatchRequest patchRequest);
    Task DeleteGroupAsync(Guid groupId);

    // Synchronization
    Task<ScimSyncResultDto> SynchronizeAsync(Guid configurationId);
}
