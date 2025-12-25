using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using USP.Core.Models.DTOs.PAM;
using USP.Core.Models.Entities;
using USP.Core.Services.Audit;
using USP.Core.Services.Communication;
using USP.Core.Services.PAM;
using USP.Infrastructure.Data;

namespace USP.Infrastructure.Services.PAM;

public class JitAccessService : IJitAccessService
{
    private readonly ApplicationDbContext _context;
    private readonly IAuditService _auditService;
    private readonly IEmailService _emailService;
    private readonly ILogger<JitAccessService> _logger;

    public JitAccessService(
        ApplicationDbContext context,
        IAuditService auditService,
        IEmailService emailService,
        ILogger<JitAccessService> logger)
    {
        _context = context;
        _auditService = auditService;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<JitAccessDto> RequestAccessAsync(Guid userId, RequestJitAccessRequest request)
    {
        // Validate resource exists
        await ValidateResourceAsync(request.ResourceType, request.ResourceId);

        // Get template if specified
        JitAccessTemplate? template = null;
        if (request.TemplateId.HasValue)
        {
            template = await _context.JitAccessTemplates.FindAsync(request.TemplateId.Value);
            if (template == null || !template.Active)
                throw new InvalidOperationException("Invalid or inactive template");

            // Validate duration against template constraints
            if (request.DurationMinutes < template.MinDurationMinutes ||
                request.DurationMinutes > template.MaxDurationMinutes)
            {
                throw new InvalidOperationException(
                    $"Duration must be between {template.MinDurationMinutes} and {template.MaxDurationMinutes} minutes");
            }

            // Check if user's role is allowed
            if (!string.IsNullOrEmpty(template.AllowedRoles))
            {
                var allowedRoles = JsonSerializer.Deserialize<List<Guid>>(template.AllowedRoles) ?? new List<Guid>();
                var userRoles = await _context.UserRoles
                    .Where(ur => ur.UserId == userId)
                    .Select(ur => ur.RoleId)
                    .ToListAsync();

                if (allowedRoles.Any() && !allowedRoles.Intersect(userRoles).Any())
                    throw new InvalidOperationException("User role not allowed for this template");
            }
        }

        // Create JIT access request
        var jitAccess = new JitAccess
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ResourceType = request.ResourceType,
            ResourceId = request.ResourceId,
            ResourceName = await GetResourceNameAsync(request.ResourceType, request.ResourceId),
            AccessLevel = request.AccessLevel,
            Justification = request.Justification,
            TemplateId = request.TemplateId,
            RequestedAt = DateTime.UtcNow,
            DurationMinutes = request.DurationMinutes,
            Status = "pending"
        };

        // Check if approval is required
        var requiresApproval = template?.RequiresApproval ?? await RequiresApprovalAsync(request.ResourceType, request.ResourceId);

        if (requiresApproval)
        {
            // Create approval request
            var approvers = template != null && template.Approvers != null && template.Approvers.Any()
                ? JsonSerializer.Deserialize<List<Guid>>(template.Approvers) ?? new List<Guid>()
                : await GetDefaultApproversAsync(request.ResourceType, request.ResourceId);

            var approval = new AccessApproval
            {
                Id = Guid.NewGuid(),
                RequesterId = userId,
                ResourceType = "JitAccess",
                ResourceId = jitAccess.Id,
                Reason = request.Justification,
                Status = "pending",
                ApprovalPolicy = template?.ApprovalPolicy ?? "single_approver",
                RequiredApprovals = template?.ApprovalPolicy == "dual_control" ? 2 : 1,
                Approvers = approvers,
                ApprovedBy = new List<Guid>(),
                RequestedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(24)
            };

            _context.AccessApprovals.Add(approval);
            jitAccess.ApprovalId = approval.Id;

            // Send email notifications to approvers
            await NotifyApproversAsync(approvers, jitAccess);
        }
        else
        {
            // Grant access immediately
            jitAccess.Status = "active";
            jitAccess.GrantedAt = DateTime.UtcNow;
            jitAccess.ExpiresAt = DateTime.UtcNow.AddMinutes(request.DurationMinutes);

            // Auto-provision access
            await ProvisionAccessAsync(jitAccess);
        }

        _context.JitAccesses.Add(jitAccess);

        // Update template usage
        if (template != null)
        {
            template.UsageCount++;
            template.LastUsed = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        // Audit log
        await _auditService.LogAsync(
            userId,
            "jit_access_requested",
            "JitAccess",
            jitAccess.Id.ToString(),
            null,
            new
            {
                resourceType = request.ResourceType,
                resourceId = request.ResourceId,
                accessLevel = request.AccessLevel,
                durationMinutes = request.DurationMinutes,
                requiresApproval
            });

        _logger.LogInformation(
            "JIT access requested: {AccessId} by user {UserId} for {ResourceType}/{ResourceId}",
            jitAccess.Id,
            userId,
            request.ResourceType,
            request.ResourceId);

        return await MapToDto(jitAccess);
    }

    public async Task<JitAccessDto?> GetAccessByIdAsync(Guid accessId, Guid userId)
    {
        var access = await _context.JitAccesses
            .Include(j => j.User)
            .Include(j => j.Template)
            .Include(j => j.Approval)
            .Include(j => j.RevokedByUser)
            .FirstOrDefaultAsync(j => j.Id == accessId);

        if (access == null)
            return null;

        // Check if user can view this access
        if (access.UserId != userId && !await IsAdminAsync(userId))
            return null;

        return await MapToDto(access);
    }

    public async Task<List<JitAccessDto>> GetActiveAccessGrantsAsync(Guid userId)
    {
        var grants = await _context.JitAccesses
            .Include(j => j.User)
            .Include(j => j.Template)
            .Where(j => j.UserId == userId && j.Status == "active")
            .OrderByDescending(j => j.GrantedAt)
            .ToListAsync();

        var dtos = new List<JitAccessDto>();
        foreach (var grant in grants)
        {
            dtos.Add(await MapToDto(grant));
        }

        return dtos;
    }

    public async Task<List<JitAccessDto>> GetUserAccessGrantsAsync(Guid userId, int? limit = 50)
    {
        var grants = await _context.JitAccesses
            .Include(j => j.User)
            .Include(j => j.Template)
            .Include(j => j.RevokedByUser)
            .Where(j => j.UserId == userId)
            .OrderByDescending(j => j.RequestedAt)
            .Take(limit ?? 50)
            .ToListAsync();

        var dtos = new List<JitAccessDto>();
        foreach (var grant in grants)
        {
            dtos.Add(await MapToDto(grant));
        }

        return dtos;
    }

    public async Task<List<JitAccessDto>> GetPendingRequestsAsync(Guid userId)
    {
        // Get approvals where user is an approver
        var approvals = await _context.AccessApprovals
            .Where(a => a.Status == "pending" && a.ResourceType == "JitAccess")
            .ToListAsync();

        var pendingAccessIds = new List<Guid>();
        foreach (var approval in approvals)
        {
            if (approval.Approvers != null && approval.Approvers.Contains(userId))
            {
                pendingAccessIds.Add(Guid.Parse(approval.ResourceId.ToString()));
            }
        }

        var grants = await _context.JitAccesses
            .Include(j => j.User)
            .Include(j => j.Template)
            .Where(j => pendingAccessIds.Contains(j.Id))
            .OrderByDescending(j => j.RequestedAt)
            .ToListAsync();

        var dtos = new List<JitAccessDto>();
        foreach (var grant in grants)
        {
            dtos.Add(await MapToDto(grant));
        }

        return dtos;
    }

    public async Task<bool> RevokeAccessAsync(Guid accessId, Guid userId, string reason)
    {
        var access = await _context.JitAccesses.FindAsync(accessId);
        if (access == null)
            return false;

        // Check if user can revoke (owner or admin)
        if (access.UserId != userId && !await IsAdminAsync(userId))
            return false;

        if (access.Status != "active")
            return false;

        access.Status = "revoked";
        access.RevokedAt = DateTime.UtcNow;
        access.RevokedBy = userId;
        access.RevocationReason = reason;

        // Auto-deprovision access
        await DeprovisionAccessAsync(access);

        await _context.SaveChangesAsync();

        // Audit log
        await _auditService.LogAsync(
            userId,
            "jit_access_revoked",
            "JitAccess",
            accessId.ToString(),
            null,
            new
            {
                reason,
                originalUserId = access.UserId,
                resourceType = access.ResourceType,
                resourceId = access.ResourceId
            });

        _logger.LogInformation(
            "JIT access revoked: {AccessId} by user {UserId}",
            accessId,
            userId);

        return true;
    }

    public async Task<bool> ApproveAccessAsync(Guid accessId, Guid approverId)
    {
        var access = await _context.JitAccesses
            .Include(j => j.Approval)
            .FirstOrDefaultAsync(j => j.Id == accessId);

        if (access == null || access.Approval == null)
            return false;

        if (access.Status != "pending")
            return false;

        var approval = access.Approval;

        // Check if user is an approver
        if (approval.Approvers == null || !approval.Approvers.Contains(approverId))
            return false;

        // Add to approved list
        if (!approval.ApprovedBy.Contains(approverId))
        {
            approval.ApprovedBy.Add(approverId);
            approval.CurrentApprovals++;
        }

        // Check if approval is complete
        if (approval.CurrentApprovals >= approval.RequiredApprovals)
        {
            approval.Status = "approved";
            approval.ApprovedAt = DateTime.UtcNow;

            // Grant access
            access.Status = "active";
            access.GrantedAt = DateTime.UtcNow;
            access.ExpiresAt = DateTime.UtcNow.AddMinutes(access.DurationMinutes);

            // Auto-provision access
            await ProvisionAccessAsync(access);

            // Notify user
            await NotifyUserAsync(access.UserId, "JIT Access Approved",
                $"Your JIT access request for {access.ResourceName} has been approved.");
        }

        await _context.SaveChangesAsync();

        // Audit log
        await _auditService.LogAsync(
            approverId,
            "jit_access_approved",
            "JitAccess",
            accessId.ToString(),
            null,
            new
            {
                requesterId = access.UserId,
                resourceType = access.ResourceType,
                resourceId = access.ResourceId,
                approvalComplete = approval.Status == "approved"
            });

        return true;
    }

    public async Task<bool> DenyAccessAsync(Guid accessId, Guid approverId, string reason)
    {
        var access = await _context.JitAccesses
            .Include(j => j.Approval)
            .FirstOrDefaultAsync(j => j.Id == accessId);

        if (access == null || access.Approval == null)
            return false;

        if (access.Status != "pending")
            return false;

        var approval = access.Approval;

        // Check if user is an approver
        if (approval.Approvers == null || !approval.Approvers.Contains(approverId))
            return false;

        approval.Status = "denied";
        approval.DeniedBy = approverId.ToString();
        approval.DenialReason = reason;
        approval.DeniedAt = DateTime.UtcNow;

        access.Status = "denied";

        // Notify user
        await NotifyUserAsync(access.UserId, "JIT Access Denied",
            $"Your JIT access request for {access.ResourceName} has been denied. Reason: {reason}");

        await _context.SaveChangesAsync();

        // Audit log
        await _auditService.LogAsync(
            approverId,
            "jit_access_denied",
            "JitAccess",
            accessId.ToString(),
            null,
            new
            {
                requesterId = access.UserId,
                resourceType = access.ResourceType,
                resourceId = access.ResourceId,
                reason
            });

        return true;
    }

    public async Task<int> ProcessExpiredGrantsAsync()
    {
        var now = DateTime.UtcNow;

        var expiredGrants = await _context.JitAccesses
            .Where(j => j.Status == "active" && j.ExpiresAt.HasValue && j.ExpiresAt.Value <= now)
            .ToListAsync();

        if (expiredGrants.Count == 0)
            return 0;

        foreach (var grant in expiredGrants)
        {
            grant.Status = "expired";

            // Auto-deprovision access
            await DeprovisionAccessAsync(grant);

            // Notify user
            await NotifyUserAsync(grant.UserId, "JIT Access Expired",
                $"Your JIT access to {grant.ResourceName} has expired.");

            _logger.LogInformation(
                "JIT access expired: {AccessId}",
                grant.Id);
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Processed {Count} expired JIT access grants",
            expiredGrants.Count);

        return expiredGrants.Count;
    }

    public async Task<JitAccessStatisticsDto> GetStatisticsAsync(Guid userId)
    {
        var isAdmin = await IsAdminAsync(userId);

        var query = _context.JitAccesses.AsQueryable();
        if (!isAdmin)
        {
            query = query.Where(j => j.UserId == userId);
        }

        var grants = await query
            .Include(j => j.User)
            .Include(j => j.Template)
            .ToListAsync();

        var now = DateTime.UtcNow;
        var totalRequests = grants.Count;
        var activeGrants = grants.Count(j => j.Status == "active");
        var pendingApprovals = grants.Count(j => j.Status == "pending");
        var expiredGrants = grants.Count(j => j.Status == "expired");
        var revokedGrants = grants.Count(j => j.Status == "revoked");
        var deniedRequests = grants.Count(j => j.Status == "denied");
        var grantsLast24Hours = grants.Count(j => j.RequestedAt >= now.AddHours(-24));
        var grantsLast7Days = grants.Count(j => j.RequestedAt >= now.AddDays(-7));
        var grantsLast30Days = grants.Count(j => j.RequestedAt >= now.AddDays(-30));

        var completedGrants = grants.Where(j => j.GrantedAt.HasValue && j.ExpiresAt.HasValue).ToList();
        var avgDuration = completedGrants.Any()
            ? TimeSpan.FromMinutes(completedGrants.Average(j => j.DurationMinutes))
            : TimeSpan.Zero;

        var accessByResourceType = grants
            .GroupBy(j => j.ResourceType)
            .Select(g => new JitAccessByResourceTypeDto
            {
                ResourceType = g.Key,
                Count = g.Count(),
                ActiveCount = g.Count(j => j.Status == "active")
            })
            .ToList();

        var topUsers = grants
            .GroupBy(j => new { j.UserId, j.User.Email })
            .Select(g => new JitAccessByUserDto
            {
                UserId = g.Key.UserId,
                UserEmail = g.Key.Email ?? string.Empty,
                RequestCount = g.Count(),
                ActiveGrants = g.Count(j => j.Status == "active")
            })
            .OrderByDescending(u => u.RequestCount)
            .Take(10)
            .ToList();

        var templateUsage = grants
            .Where(j => j.TemplateId.HasValue)
            .GroupBy(j => new { j.TemplateId, j.Template!.Name })
            .Select(g => new JitAccessTemplateUsageDto
            {
                TemplateId = g.Key.TemplateId!.Value,
                TemplateName = g.Key.Name,
                UsageCount = g.Count(),
                LastUsed = g.Max(j => j.RequestedAt)
            })
            .OrderByDescending(t => t.UsageCount)
            .ToList();

        return new JitAccessStatisticsDto
        {
            TotalRequests = totalRequests,
            ActiveGrants = activeGrants,
            PendingApprovals = pendingApprovals,
            ExpiredGrants = expiredGrants,
            RevokedGrants = revokedGrants,
            DeniedRequests = deniedRequests,
            GrantsLast24Hours = grantsLast24Hours,
            GrantsLast7Days = grantsLast7Days,
            GrantsLast30Days = grantsLast30Days,
            AverageGrantDuration = avgDuration,
            AccessByResourceType = accessByResourceType,
            TopUsersByRequests = topUsers,
            TemplateUsage = templateUsage
        };
    }

    // Template Management

    public async Task<JitAccessTemplateDto> CreateTemplateAsync(Guid userId, CreateJitTemplateRequest request)
    {
        // Check if user is admin
        if (!await IsAdminAsync(userId))
            throw new InvalidOperationException("Only administrators can create JIT templates");

        var template = new JitAccessTemplate
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            ResourceType = request.ResourceType,
            ResourceId = request.ResourceId,
            AccessLevel = request.AccessLevel,
            DefaultDurationMinutes = request.DefaultDurationMinutes,
            MaxDurationMinutes = request.MaxDurationMinutes,
            MinDurationMinutes = request.MinDurationMinutes,
            RequiresApproval = request.RequiresApproval,
            ApprovalPolicy = request.ApprovalPolicy,
            Approvers = request.Approvers != null && request.Approvers.Any()
                ? JsonSerializer.Serialize(request.Approvers) : null,
            RequiresJustification = request.RequiresJustification,
            AllowedRoles = request.AllowedRoles != null && request.AllowedRoles.Any()
                ? JsonSerializer.Serialize(request.AllowedRoles) : null,
            Active = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.JitAccessTemplates.Add(template);
        await _context.SaveChangesAsync();

        // Audit log
        await _auditService.LogAsync(
            userId,
            "jit_template_created",
            "JitAccessTemplate",
            template.Id.ToString(),
            null,
            new
            {
                name = request.Name,
                resourceType = request.ResourceType
            });

        _logger.LogInformation(
            "JIT access template created: {TemplateId} by user {UserId}",
            template.Id,
            userId);

        return await MapTemplateToDto(template);
    }

    public async Task<JitAccessTemplateDto?> GetTemplateByIdAsync(Guid templateId)
    {
        var template = await _context.JitAccessTemplates.FindAsync(templateId);
        if (template == null)
            return null;

        return await MapTemplateToDto(template);
    }

    public async Task<List<JitAccessTemplateDto>> GetTemplatesAsync(Guid userId)
    {
        var templates = await _context.JitAccessTemplates
            .Where(t => t.Active)
            .OrderBy(t => t.Name)
            .ToListAsync();

        var dtos = new List<JitAccessTemplateDto>();
        foreach (var template in templates)
        {
            dtos.Add(await MapTemplateToDto(template));
        }

        return dtos;
    }

    public async Task<bool> UpdateTemplateAsync(Guid templateId, Guid userId, CreateJitTemplateRequest request)
    {
        if (!await IsAdminAsync(userId))
            return false;

        var template = await _context.JitAccessTemplates.FindAsync(templateId);
        if (template == null)
            return false;

        template.Name = request.Name;
        template.Description = request.Description;
        template.ResourceType = request.ResourceType;
        template.ResourceId = request.ResourceId;
        template.AccessLevel = request.AccessLevel;
        template.DefaultDurationMinutes = request.DefaultDurationMinutes;
        template.MaxDurationMinutes = request.MaxDurationMinutes;
        template.MinDurationMinutes = request.MinDurationMinutes;
        template.RequiresApproval = request.RequiresApproval;
        template.ApprovalPolicy = request.ApprovalPolicy;
        template.Approvers = request.Approvers != null && request.Approvers.Any()
            ? JsonSerializer.Serialize(request.Approvers) : null;
        template.RequiresJustification = request.RequiresJustification;
        template.AllowedRoles = request.AllowedRoles != null && request.AllowedRoles.Any()
            ? JsonSerializer.Serialize(request.AllowedRoles) : null;
        template.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Audit log
        await _auditService.LogAsync(
            userId,
            "jit_template_updated",
            "JitAccessTemplate",
            templateId.ToString(),
            null,
            new { name = request.Name });

        return true;
    }

    public async Task<bool> DeleteTemplateAsync(Guid templateId, Guid userId)
    {
        if (!await IsAdminAsync(userId))
            return false;

        var template = await _context.JitAccessTemplates.FindAsync(templateId);
        if (template == null)
            return false;

        _context.JitAccessTemplates.Remove(template);
        await _context.SaveChangesAsync();

        // Audit log
        await _auditService.LogAsync(
            userId,
            "jit_template_deleted",
            "JitAccessTemplate",
            templateId.ToString(),
            null,
            new { name = template.Name });

        return true;
    }

    public async Task<bool> ToggleTemplateActiveAsync(Guid templateId, Guid userId, bool active)
    {
        if (!await IsAdminAsync(userId))
            return false;

        var template = await _context.JitAccessTemplates.FindAsync(templateId);
        if (template == null)
            return false;

        template.Active = active;
        template.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Audit log
        await _auditService.LogAsync(
            userId,
            active ? "jit_template_activated" : "jit_template_deactivated",
            "JitAccessTemplate",
            templateId.ToString(),
            null,
            new { name = template.Name });

        return true;
    }

    // Private helper methods

    private async Task<JitAccessDto> MapToDto(JitAccess access)
    {
        TimeSpan? remainingTime = null;
        if (access.Status == "active" && access.ExpiresAt.HasValue)
        {
            var remaining = access.ExpiresAt.Value - DateTime.UtcNow;
            remainingTime = remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }

        return new JitAccessDto
        {
            Id = access.Id,
            UserId = access.UserId,
            UserEmail = access.User?.Email ?? string.Empty,
            ResourceType = access.ResourceType,
            ResourceId = access.ResourceId,
            ResourceName = access.ResourceName,
            AccessLevel = access.AccessLevel,
            Justification = access.Justification,
            TemplateId = access.TemplateId,
            TemplateName = access.Template?.Name,
            ApprovalId = access.ApprovalId,
            RequestedAt = access.RequestedAt,
            GrantedAt = access.GrantedAt,
            ExpiresAt = access.ExpiresAt,
            RevokedAt = access.RevokedAt,
            RevokedBy = access.RevokedBy,
            RevokedByEmail = access.RevokedByUser?.Email,
            RevocationReason = access.RevocationReason,
            Status = access.Status,
            DurationMinutes = access.DurationMinutes,
            AutoProvisioningCompleted = access.AutoProvisioningCompleted,
            AutoDeprovisioningCompleted = access.AutoDeprovisioningCompleted,
            RemainingTime = remainingTime
        };
    }

    private async Task<JitAccessTemplateDto> MapTemplateToDto(JitAccessTemplate template)
    {
        var approverEmails = new List<string>();
        if (!string.IsNullOrEmpty(template.Approvers))
        {
            var approverIds = JsonSerializer.Deserialize<List<Guid>>(template.Approvers) ?? new List<Guid>();
            approverEmails = await _context.Users
                .Where(u => approverIds.Contains(u.Id))
                .Select(u => u.Email!)
                .ToListAsync();
        }

        var allowedRoleNames = new List<string>();
        if (!string.IsNullOrEmpty(template.AllowedRoles))
        {
            var roleIds = JsonSerializer.Deserialize<List<Guid>>(template.AllowedRoles) ?? new List<Guid>();
            allowedRoleNames = await _context.Roles
                .Where(r => roleIds.Contains(r.Id))
                .Select(r => r.Name!)
                .ToListAsync();
        }

        return new JitAccessTemplateDto
        {
            Id = template.Id,
            Name = template.Name,
            Description = template.Description,
            ResourceType = template.ResourceType,
            ResourceId = template.ResourceId,
            AccessLevel = template.AccessLevel,
            DefaultDurationMinutes = template.DefaultDurationMinutes,
            MaxDurationMinutes = template.MaxDurationMinutes,
            MinDurationMinutes = template.MinDurationMinutes,
            RequiresApproval = template.RequiresApproval,
            ApprovalPolicy = template.ApprovalPolicy,
            ApproverEmails = approverEmails,
            RequiresJustification = template.RequiresJustification,
            AllowedRoleNames = allowedRoleNames,
            Active = template.Active,
            UsageCount = template.UsageCount,
            LastUsed = template.LastUsed,
            CreatedAt = template.CreatedAt
        };
    }

    private async Task ValidateResourceAsync(string resourceType, Guid resourceId)
    {
        var exists = resourceType.ToLower() switch
        {
            "role" => await _context.Roles.AnyAsync(r => r.Id == resourceId),
            "safe" => await _context.PrivilegedSafes.AnyAsync(s => s.Id == resourceId),
            "account" => await _context.PrivilegedAccounts.AnyAsync(a => a.Id == resourceId),
            _ => true
        };

        if (!exists)
            throw new InvalidOperationException($"{resourceType} not found");
    }

    private async Task<string> GetResourceNameAsync(string resourceType, Guid resourceId)
    {
        return resourceType.ToLower() switch
        {
            "role" => (await _context.Roles.FindAsync(resourceId))?.Name ?? resourceId.ToString(),
            "safe" => (await _context.PrivilegedSafes.FindAsync(resourceId))?.Name ?? resourceId.ToString(),
            "account" => (await _context.PrivilegedAccounts.FindAsync(resourceId))?.AccountName ?? resourceId.ToString(),
            _ => resourceId.ToString()
        };
    }

    private async Task<bool> RequiresApprovalAsync(string resourceType, Guid resourceId)
    {
        // Check if resource requires approval for JIT access
        if (resourceType.Equals("safe", StringComparison.OrdinalIgnoreCase))
        {
            var safe = await _context.PrivilegedSafes.FindAsync(resourceId);
            return safe?.RequireApproval ?? false;
        }

        return false;
    }

    private async Task<List<Guid>> GetDefaultApproversAsync(string resourceType, Guid resourceId)
    {
        // Get default approvers based on resource type
        if (resourceType.Equals("safe", StringComparison.OrdinalIgnoreCase))
        {
            var safe = await _context.PrivilegedSafes.FindAsync(resourceId);
            if (safe != null)
            {
                return new List<Guid> { safe.OwnerId };
            }
        }

        // Default: get all admin users
        var adminRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Admin");
        if (adminRole != null)
        {
            return await _context.UserRoles
                .Where(ur => ur.RoleId == adminRole.Id)
                .Select(ur => ur.UserId)
                .ToListAsync();
        }

        return new List<Guid>();
    }

    private async Task ProvisionAccessAsync(JitAccess access)
    {
        // Auto-provision access based on resource type
        var details = new
        {
            provisonedAt = DateTime.UtcNow,
            resourceType = access.ResourceType,
            resourceId = access.ResourceId,
            accessLevel = access.AccessLevel
        };

        access.ProvisioningDetails = JsonSerializer.Serialize(details);
        access.AutoProvisioningCompleted = true;

        _logger.LogInformation(
            "Auto-provisioned JIT access: {AccessId}",
            access.Id);
    }

    private async Task DeprovisionAccessAsync(JitAccess access)
    {
        // Auto-deprovision access
        var details = new
        {
            deprovisionedAt = DateTime.UtcNow,
            resourceType = access.ResourceType,
            resourceId = access.ResourceId
        };

        access.DeprovisioningDetails = JsonSerializer.Serialize(details);
        access.AutoDeprovisioningCompleted = true;

        _logger.LogInformation(
            "Auto-deprovisioned JIT access: {AccessId}",
            access.Id);
    }

    private async Task<bool> IsAdminAsync(Guid userId)
    {
        var adminRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Admin");
        if (adminRole == null)
            return false;

        return await _context.UserRoles.AnyAsync(ur => ur.UserId == userId && ur.RoleId == adminRole.Id);
    }

    private async Task NotifyApproversAsync(List<Guid> approvers, JitAccess access)
    {
        foreach (var approverId in approvers)
        {
            var approver = await _context.Users.FindAsync(approverId);
            if (approver?.Email != null)
            {
                await _emailService.SendSecurityAlertAsync(
                    approver.Email,
                    $"A JIT access request requires your approval. Resource: {access.ResourceName}, Justification: {access.Justification}",
                    approver.UserName ?? approver.Email);
            }
        }
    }

    private async Task NotifyUserAsync(Guid userId, string subject, string message)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user?.Email != null)
        {
            await _emailService.SendSecurityAlertAsync(user.Email, message, user.UserName ?? user.Email);
        }
    }
}
