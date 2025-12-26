using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using USP.Core.Models.DTOs.Workspace;
using USP.Core.Models.Entities;
using USP.Core.Services.Workspace;
using USP.Infrastructure.Data;

namespace USP.Infrastructure.Services.Workspace;

/// <summary>
/// Workspace service implementation for multi-tenant workspace management
/// </summary>
public class WorkspaceService : IWorkspaceService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<WorkspaceService> _logger;

    public WorkspaceService(
        ApplicationDbContext context,
        ILogger<WorkspaceService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<WorkspaceDto> CreateWorkspaceAsync(CreateWorkspaceRequest request, Guid ownerId)
    {
        _logger.LogInformation("Creating workspace: {WorkspaceName}, Owner: {OwnerId}", request.Name, ownerId);

        try
        {
            var slug = GenerateSlug(request.Name);

            var existingSlug = await _context.Workspaces
                .AnyAsync(w => w.Slug == slug);

            if (existingSlug)
            {
                slug = $"{slug}-{Guid.NewGuid().ToString("N").Substring(0, 6)}";
            }

            var workspace = new Core.Models.Entities.Workspace
            {
                Id = Guid.NewGuid(),
                Name = request.Name,
                Slug = slug,
                Description = request.Description,
                CustomDomain = request.CustomDomain,
                OwnerId = ownerId,
                Status = "Active",
                CreatedAt = DateTime.UtcNow
            };

            _context.Workspaces.Add(workspace);

            var ownerMember = new WorkspaceMember
            {
                Id = Guid.NewGuid(),
                WorkspaceId = workspace.Id,
                UserId = ownerId,
                Role = "Owner",
                InvitedBy = ownerId,
                InvitedAt = DateTime.UtcNow,
                JoinedAt = DateTime.UtcNow
            };

            _context.WorkspaceMembers.Add(ownerMember);

            var quota = new WorkspaceQuota
            {
                Id = Guid.NewGuid(),
                WorkspaceId = workspace.Id,
                MaxUsers = request.MaxUsers ?? 100,
                MaxSecrets = request.MaxSecrets ?? 1000,
                MaxApiKeys = request.MaxApiKeys ?? 50,
                MaxSafes = request.MaxSafes ?? 10,
                CreatedAt = DateTime.UtcNow
            };

            _context.WorkspaceQuotas.Add(quota);

            var usage = new WorkspaceUsage
            {
                Id = Guid.NewGuid(),
                WorkspaceId = workspace.Id,
                UsersCount = 1,
                SecretsCount = 0,
                ApiKeysCount = 0,
                SafesCount = 0,
                LastUpdated = DateTime.UtcNow
            };

            _context.WorkspaceUsages.Add(usage);

            await _context.SaveChangesAsync();

            _logger.LogInformation("Workspace created: {WorkspaceId}, Slug: {Slug}", workspace.Id, slug);

            return MapToDto(workspace);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create workspace: {WorkspaceName}", request.Name);
            throw;
        }
    }

    public async Task<WorkspaceDto?> GetWorkspaceAsync(Guid workspaceId)
    {
        var workspace = await _context.Workspaces
            .FirstOrDefaultAsync(w => w.Id == workspaceId && !w.IsDeleted);

        if (workspace == null)
        {
            return null;
        }

        return MapToDto(workspace);
    }

    public async Task<WorkspaceDto?> GetWorkspaceBySlugAsync(string slug)
    {
        var workspace = await _context.Workspaces
            .FirstOrDefaultAsync(w => w.Slug == slug && !w.IsDeleted);

        if (workspace == null)
        {
            return null;
        }

        return MapToDto(workspace);
    }

    public async Task<WorkspaceDto?> GetWorkspaceByDomainAsync(string domain)
    {
        var workspace = await _context.Workspaces
            .FirstOrDefaultAsync(w => w.CustomDomain == domain && !w.IsDeleted);

        if (workspace == null)
        {
            return null;
        }

        return MapToDto(workspace);
    }

    public async Task<WorkspaceDto> UpdateWorkspaceAsync(Guid workspaceId, UpdateWorkspaceRequest request)
    {
        _logger.LogInformation("Updating workspace: {WorkspaceId}", workspaceId);

        var workspace = await _context.Workspaces
            .FirstOrDefaultAsync(w => w.Id == workspaceId && !w.IsDeleted);

        if (workspace == null)
        {
            throw new InvalidOperationException($"Workspace {workspaceId} not found");
        }

        workspace.Name = request.Name ?? workspace.Name;
        workspace.Description = request.Description ?? workspace.Description;
        workspace.CustomDomain = request.CustomDomain ?? workspace.CustomDomain;
        workspace.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Workspace updated: {WorkspaceId}", workspaceId);

        return MapToDto(workspace);
    }

    public async Task DeleteWorkspaceAsync(Guid workspaceId)
    {
        _logger.LogInformation("Deleting workspace: {WorkspaceId}", workspaceId);

        var workspace = await _context.Workspaces
            .FirstOrDefaultAsync(w => w.Id == workspaceId);

        if (workspace == null)
        {
            throw new InvalidOperationException($"Workspace {workspaceId} not found");
        }

        workspace.IsDeleted = true;
        workspace.DeletedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Workspace deleted: {WorkspaceId}", workspaceId);
    }

    public async Task<List<WorkspaceDto>> GetUserWorkspacesAsync(Guid userId)
    {
        var workspaceIds = await _context.WorkspaceMembers
            .Where(wm => wm.UserId == userId)
            .Select(wm => wm.WorkspaceId)
            .ToListAsync();

        var workspaces = await _context.Workspaces
            .Where(w => workspaceIds.Contains(w.Id) && !w.IsDeleted)
            .ToListAsync();

        return workspaces.Select(MapToDto).ToList();
    }

    public async Task<WorkspaceMemberDto> AddMemberAsync(Guid workspaceId, AddWorkspaceMemberRequest request, Guid invitedBy)
    {
        _logger.LogInformation("Adding member to workspace. WorkspaceId: {WorkspaceId}, UserId: {UserId}", workspaceId, request.UserId);

        try
        {
            var existingMember = await _context.WorkspaceMembers
                .FirstOrDefaultAsync(wm => wm.WorkspaceId == workspaceId && wm.UserId == request.UserId);

            if (existingMember != null)
            {
                throw new InvalidOperationException("User is already a member of this workspace");
            }

            var invitationToken = Guid.NewGuid().ToString("N");

            var member = new WorkspaceMember
            {
                Id = Guid.NewGuid(),
                WorkspaceId = workspaceId,
                UserId = request.UserId,
                Role = request.Role,
                InvitedBy = invitedBy,
                InvitedAt = DateTime.UtcNow,
                InvitationToken = invitationToken
            };

            _context.WorkspaceMembers.Add(member);

            await IncrementUsageAsync(workspaceId, "users");

            await _context.SaveChangesAsync();

            _logger.LogInformation("Member added to workspace. WorkspaceId: {WorkspaceId}, UserId: {UserId}, Role: {Role}",
                workspaceId, request.UserId, request.Role);

            return new WorkspaceMemberDto
            {
                UserId = member.UserId,
                Role = member.Role,
                JoinedAt = member.JoinedAt
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add member to workspace. WorkspaceId: {WorkspaceId}, UserId: {UserId}", workspaceId, request.UserId);
            throw;
        }
    }

    public async Task RemoveMemberAsync(Guid workspaceId, Guid userId)
    {
        _logger.LogInformation("Removing member from workspace. WorkspaceId: {WorkspaceId}, UserId: {UserId}", workspaceId, userId);

        var member = await _context.WorkspaceMembers
            .FirstOrDefaultAsync(wm => wm.WorkspaceId == workspaceId && wm.UserId == userId);

        if (member == null)
        {
            throw new InvalidOperationException("User is not a member of this workspace");
        }

        if (member.Role == "Owner")
        {
            throw new InvalidOperationException("Cannot remove workspace owner");
        }

        _context.WorkspaceMembers.Remove(member);

        await DecrementUsageAsync(workspaceId, "users");

        await _context.SaveChangesAsync();

        _logger.LogInformation("Member removed from workspace. WorkspaceId: {WorkspaceId}, UserId: {UserId}", workspaceId, userId);
    }

    public async Task<WorkspaceMemberDto> UpdateMemberRoleAsync(Guid workspaceId, Guid userId, string role)
    {
        _logger.LogInformation("Updating member role. WorkspaceId: {WorkspaceId}, UserId: {UserId}, NewRole: {Role}", workspaceId, userId, role);

        var member = await _context.WorkspaceMembers
            .FirstOrDefaultAsync(wm => wm.WorkspaceId == workspaceId && wm.UserId == userId);

        if (member == null)
        {
            throw new InvalidOperationException("User is not a member of this workspace");
        }

        member.Role = role;
        member.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Member role updated. WorkspaceId: {WorkspaceId}, UserId: {UserId}, Role: {Role}", workspaceId, userId, role);

        return new WorkspaceMemberDto
        {
            UserId = member.UserId,
            Role = member.Role,
            JoinedAt = member.JoinedAt
        };
    }

    public async Task<List<WorkspaceMemberDto>> GetMembersAsync(Guid workspaceId)
    {
        var members = await _context.WorkspaceMembers
            .Where(wm => wm.WorkspaceId == workspaceId)
            .ToListAsync();

        return members.Select(m => new WorkspaceMemberDto
        {
            UserId = m.UserId,
            Role = m.Role,
            JoinedAt = m.JoinedAt
        }).ToList();
    }

    public async Task<bool> HasAccessAsync(Guid userId, Guid workspaceId)
    {
        return await _context.WorkspaceMembers
            .AnyAsync(wm => wm.UserId == userId && wm.WorkspaceId == workspaceId);
    }

    public async Task<string?> GetUserRoleAsync(Guid userId, Guid workspaceId)
    {
        var member = await _context.WorkspaceMembers
            .FirstOrDefaultAsync(wm => wm.UserId == userId && wm.WorkspaceId == workspaceId);

        return member?.Role;
    }

    public async Task<bool> CheckQuotaAsync(Guid workspaceId, string quotaType)
    {
        var quota = await _context.WorkspaceQuotas
            .FirstOrDefaultAsync(q => q.WorkspaceId == workspaceId);

        var usage = await _context.WorkspaceUsages
            .FirstOrDefaultAsync(u => u.WorkspaceId == workspaceId);

        if (quota == null || usage == null)
        {
            return false;
        }

        return quotaType.ToLower() switch
        {
            "users" => usage.UsersCount < quota.MaxUsers,
            "secrets" => usage.SecretsCount < quota.MaxSecrets,
            "apikeys" => usage.ApiKeysCount < quota.MaxApiKeys,
            "safes" => usage.SafesCount < quota.MaxSafes,
            _ => false
        };
    }

    public async Task IncrementUsageAsync(Guid workspaceId, string usageType)
    {
        var usage = await _context.WorkspaceUsages
            .FirstOrDefaultAsync(u => u.WorkspaceId == workspaceId);

        if (usage == null)
        {
            usage = new WorkspaceUsage
            {
                Id = Guid.NewGuid(),
                WorkspaceId = workspaceId,
                LastUpdated = DateTime.UtcNow
            };
            _context.WorkspaceUsages.Add(usage);
        }

        switch (usageType.ToLower())
        {
            case "users":
                usage.UsersCount++;
                break;
            case "secrets":
                usage.SecretsCount++;
                break;
            case "apikeys":
                usage.ApiKeysCount++;
                break;
            case "safes":
                usage.SafesCount++;
                break;
        }

        usage.LastUpdated = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }

    public async Task DecrementUsageAsync(Guid workspaceId, string usageType)
    {
        var usage = await _context.WorkspaceUsages
            .FirstOrDefaultAsync(u => u.WorkspaceId == workspaceId);

        if (usage == null)
        {
            return;
        }

        switch (usageType.ToLower())
        {
            case "users":
                usage.UsersCount = Math.Max(0, usage.UsersCount - 1);
                break;
            case "secrets":
                usage.SecretsCount = Math.Max(0, usage.SecretsCount - 1);
                break;
            case "apikeys":
                usage.ApiKeysCount = Math.Max(0, usage.ApiKeysCount - 1);
                break;
            case "safes":
                usage.SafesCount = Math.Max(0, usage.SafesCount - 1);
                break;
        }

        usage.LastUpdated = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }

    public async Task<WorkspaceQuotaUsageDto> GetQuotaUsageAsync(Guid workspaceId)
    {
        var quota = await _context.WorkspaceQuotas
            .FirstOrDefaultAsync(q => q.WorkspaceId == workspaceId);

        var usage = await _context.WorkspaceUsages
            .FirstOrDefaultAsync(u => u.WorkspaceId == workspaceId);

        if (quota == null || usage == null)
        {
            throw new InvalidOperationException($"Quota or usage not found for workspace {workspaceId}");
        }

        return new WorkspaceQuotaUsageDto
        {
            WorkspaceId = workspaceId,
            UsersCount = usage.UsersCount,
            MaxUsers = quota.MaxUsers,
            SecretsCount = usage.SecretsCount,
            MaxSecrets = quota.MaxSecrets,
            ApiKeysCount = usage.ApiKeysCount,
            MaxApiKeys = quota.MaxApiKeys,
            SafesCount = usage.SafesCount,
            MaxSafes = quota.MaxSafes
        };
    }

    public async Task UpdateQuotaAsync(Guid workspaceId, UpdateWorkspaceQuotaRequest request)
    {
        _logger.LogInformation("Updating workspace quota. WorkspaceId: {WorkspaceId}", workspaceId);

        var quota = await _context.WorkspaceQuotas
            .FirstOrDefaultAsync(q => q.WorkspaceId == workspaceId);

        if (quota == null)
        {
            throw new InvalidOperationException($"Quota not found for workspace {workspaceId}");
        }

        if (request.MaxUsers.HasValue) quota.MaxUsers = request.MaxUsers.Value;
        if (request.MaxSecrets.HasValue) quota.MaxSecrets = request.MaxSecrets.Value;
        if (request.MaxApiKeys.HasValue) quota.MaxApiKeys = request.MaxApiKeys.Value;
        if (request.MaxSafes.HasValue) quota.MaxSafes = request.MaxSafes.Value;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Workspace quota updated. WorkspaceId: {WorkspaceId}", workspaceId);
    }

    public async Task AcceptInvitationAsync(string invitationToken, Guid userId)
    {
        _logger.LogInformation("Accepting workspace invitation. Token: {Token}, UserId: {UserId}", invitationToken, userId);

        var member = await _context.WorkspaceMembers
            .FirstOrDefaultAsync(wm => wm.InvitationToken == invitationToken && wm.UserId == userId);

        if (member == null)
        {
            throw new InvalidOperationException("Invalid invitation token");
        }

        member.JoinedAt = DateTime.UtcNow;
        member.InvitationToken = null;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Workspace invitation accepted. WorkspaceId: {WorkspaceId}, UserId: {UserId}", member.WorkspaceId, userId);
    }

    public async Task SuspendWorkspaceAsync(Guid workspaceId, string reason)
    {
        _logger.LogInformation("Suspending workspace. WorkspaceId: {WorkspaceId}, Reason: {Reason}", workspaceId, reason);

        var workspace = await _context.Workspaces
            .FirstOrDefaultAsync(w => w.Id == workspaceId);

        if (workspace == null)
        {
            throw new InvalidOperationException($"Workspace {workspaceId} not found");
        }

        workspace.Status = "Suspended";
        workspace.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Workspace suspended. WorkspaceId: {WorkspaceId}", workspaceId);
    }

    public async Task ActivateWorkspaceAsync(Guid workspaceId)
    {
        _logger.LogInformation("Activating workspace. WorkspaceId: {WorkspaceId}", workspaceId);

        var workspace = await _context.Workspaces
            .FirstOrDefaultAsync(w => w.Id == workspaceId);

        if (workspace == null)
        {
            throw new InvalidOperationException($"Workspace {workspaceId} not found");
        }

        workspace.Status = "Active";
        workspace.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Workspace activated. WorkspaceId: {WorkspaceId}", workspaceId);
    }

    // ============================================
    // Private Helper Methods
    // ============================================

    private string GenerateSlug(string name)
    {
        return name.ToLowerInvariant()
            .Replace(" ", "-")
            .Replace("_", "-")
            .Trim('-');
    }

    private WorkspaceDto MapToDto(Core.Models.Entities.Workspace workspace)
    {
        return new WorkspaceDto
        {
            Id = workspace.Id,
            Name = workspace.Name,
            Slug = workspace.Slug,
            Description = workspace.Description,
            Status = workspace.Status,
            CreatedAt = workspace.CreatedAt
        };
    }
}
