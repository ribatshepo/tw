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

public class BreakGlassService : IBreakGlassService
{
    private readonly ApplicationDbContext _context;
    private readonly IAuditService _auditService;
    private readonly IEmailService _emailService;
    private readonly ILogger<BreakGlassService> _logger;

    public BreakGlassService(
        ApplicationDbContext context,
        IAuditService auditService,
        IEmailService emailService,
        ILogger<BreakGlassService> logger)
    {
        _context = context;
        _auditService = auditService;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<BreakGlassAccessDto> ActivateAsync(Guid userId, ActivateBreakGlassRequest request)
    {
        // Get active policy
        var policy = await _context.BreakGlassPolicies
            .FirstOrDefaultAsync(p => p.Enabled);

        if (policy == null)
            throw new InvalidOperationException("No active break-glass policy found");

        // Validate request against policy
        if (policy.RequireJustification && string.IsNullOrWhiteSpace(request.Reason))
            throw new InvalidOperationException("Justification is required");

        if (policy.RequireJustification && request.Reason.Length < policy.MinJustificationLength)
            throw new InvalidOperationException($"Justification must be at least {policy.MinJustificationLength} characters");

        if (request.DurationMinutes > policy.MaxDurationMinutes)
            throw new InvalidOperationException($"Duration cannot exceed {policy.MaxDurationMinutes} minutes");

        // Check if allowed incident types are configured
        if (!string.IsNullOrEmpty(policy.AllowedIncidentTypes))
        {
            var allowedTypes = JsonSerializer.Deserialize<List<string>>(policy.AllowedIncidentTypes) ?? new List<string>();
            if (allowedTypes.Any() && !allowedTypes.Contains(request.IncidentType, StringComparer.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Incident type '{request.IncidentType}' is not allowed");
        }

        // Check if user's role is allowed
        if (!string.IsNullOrEmpty(policy.RestrictedToRoles))
        {
            var allowedRoles = JsonSerializer.Deserialize<List<Guid>>(policy.RestrictedToRoles) ?? new List<Guid>();
            var userRoles = await _context.UserRoles
                .Where(ur => ur.UserId == userId)
                .Select(ur => ur.RoleId)
                .ToListAsync();

            if (allowedRoles.Any() && !allowedRoles.Intersect(userRoles).Any())
                throw new InvalidOperationException("Your role is not authorized to use break-glass access");
        }

        // Check if user already has active break-glass access
        var existingActive = await _context.BreakGlassAccesses
            .FirstOrDefaultAsync(b => b.UserId == userId && b.Status == "active");

        if (existingActive != null)
            throw new InvalidOperationException("You already have an active break-glass access");

        // Create break-glass access
        var access = new BreakGlassAccess
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Reason = request.Reason,
            IncidentType = request.IncidentType,
            Severity = request.Severity,
            ActivatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(request.DurationMinutes),
            Status = "active",
            DurationMinutes = request.DurationMinutes,
            SessionRecordingMandatory = policy.MandatorySessionRecording,
            RequiresReview = policy.RequirePostAccessReview
        };

        _context.BreakGlassAccesses.Add(access);
        await _context.SaveChangesAsync();

        // Notify executives
        if (policy.AutoNotifyExecutives && !string.IsNullOrEmpty(policy.ExecutiveUserIds))
        {
            var executiveIds = JsonSerializer.Deserialize<List<Guid>>(policy.ExecutiveUserIds) ?? new List<Guid>();
            await NotifyExecutivesAsync(executiveIds, access);
            access.ExecutiveNotified = true;
            access.ExecutiveNotifiedAt = DateTime.UtcNow;
            access.NotifiedExecutives = policy.ExecutiveUserIds;
            await _context.SaveChangesAsync();
        }

        // Audit log
        await _auditService.LogAsync(
            userId,
            "break_glass_activated",
            "BreakGlassAccess",
            access.Id.ToString(),
            null,
            new
            {
                incidentType = request.IncidentType,
                severity = request.Severity,
                durationMinutes = request.DurationMinutes,
                reason = request.Reason
            });

        _logger.LogWarning(
            "BREAK-GLASS ACCESS ACTIVATED: User {UserId}, Incident: {IncidentType}, Severity: {Severity}, Reason: {Reason}",
            userId,
            request.IncidentType,
            request.Severity,
            request.Reason);

        return await MapToDto(access);
    }

    public async Task<bool> DeactivateAsync(Guid accessId, Guid userId, DeactivateBreakGlassRequest request)
    {
        var access = await _context.BreakGlassAccesses
            .Include(b => b.User)
            .FirstOrDefaultAsync(b => b.Id == accessId);

        if (access == null)
            return false;

        // Check if user can deactivate (owner or admin)
        if (access.UserId != userId && !await IsAdminAsync(userId))
            return false;

        if (access.Status != "active")
            return false;

        access.Status = "deactivated";
        access.DeactivatedAt = DateTime.UtcNow;

        // Store accessed resources and actions
        if (request.AccessedResources != null && request.AccessedResources.Any())
        {
            access.AccessedResources = JsonSerializer.Serialize(request.AccessedResources);
        }

        if (request.ActionsPerformed != null && request.ActionsPerformed.Any())
        {
            access.ActionsPerformed = JsonSerializer.Serialize(request.ActionsPerformed);
        }

        // If review is required, set status to under_review
        if (access.RequiresReview)
        {
            access.Status = "under_review";
        }

        await _context.SaveChangesAsync();

        // Audit log
        await _auditService.LogAsync(
            userId,
            "break_glass_deactivated",
            "BreakGlassAccess",
            accessId.ToString(),
            null,
            new
            {
                duration = (access.DeactivatedAt.Value - access.ActivatedAt).TotalMinutes,
                accessedResources = request.AccessedResources?.Count ?? 0,
                actionsPerformed = request.ActionsPerformed?.Count ?? 0
            });

        _logger.LogWarning(
            "BREAK-GLASS ACCESS DEACTIVATED: User {UserId}, AccessId {AccessId}, Duration: {Duration} minutes",
            userId,
            accessId,
            (access.DeactivatedAt.Value - access.ActivatedAt).TotalMinutes);

        return true;
    }

    public async Task<BreakGlassAccessDto?> GetAccessByIdAsync(Guid accessId, Guid userId)
    {
        var access = await _context.BreakGlassAccesses
            .Include(b => b.User)
            .Include(b => b.Reviewer)
            .FirstOrDefaultAsync(b => b.Id == accessId);

        if (access == null)
            return null;

        // Check if user can view (owner or admin)
        if (access.UserId != userId && !await IsAdminAsync(userId))
            return null;

        return await MapToDto(access);
    }

    public async Task<BreakGlassAccessDto?> GetActiveAccessAsync(Guid userId)
    {
        var access = await _context.BreakGlassAccesses
            .Include(b => b.User)
            .FirstOrDefaultAsync(b => b.UserId == userId && b.Status == "active");

        if (access == null)
            return null;

        return await MapToDto(access);
    }

    public async Task<List<BreakGlassAccessDto>> GetUserHistoryAsync(Guid userId, int? limit = 50)
    {
        var accesses = await _context.BreakGlassAccesses
            .Include(b => b.User)
            .Include(b => b.Reviewer)
            .Where(b => b.UserId == userId)
            .OrderByDescending(b => b.ActivatedAt)
            .Take(limit ?? 50)
            .ToListAsync();

        var dtos = new List<BreakGlassAccessDto>();
        foreach (var access in accesses)
        {
            dtos.Add(await MapToDto(access));
        }

        return dtos;
    }

    public async Task<List<BreakGlassAccessDto>> GetAllHistoryAsync(Guid userId, int? limit = 100)
    {
        // Check if user is admin
        if (!await IsAdminAsync(userId))
            return new List<BreakGlassAccessDto>();

        var accesses = await _context.BreakGlassAccesses
            .Include(b => b.User)
            .Include(b => b.Reviewer)
            .OrderByDescending(b => b.ActivatedAt)
            .Take(limit ?? 100)
            .ToListAsync();

        var dtos = new List<BreakGlassAccessDto>();
        foreach (var access in accesses)
        {
            dtos.Add(await MapToDto(access));
        }

        return dtos;
    }

    public async Task<List<BreakGlassAccessDto>> GetPendingReviewAsync(Guid userId)
    {
        // Check if user is admin or executive
        if (!await IsAdminAsync(userId))
            return new List<BreakGlassAccessDto>();

        var accesses = await _context.BreakGlassAccesses
            .Include(b => b.User)
            .Where(b => b.Status == "under_review")
            .OrderBy(b => b.DeactivatedAt)
            .ToListAsync();

        var dtos = new List<BreakGlassAccessDto>();
        foreach (var access in accesses)
        {
            dtos.Add(await MapToDto(access));
        }

        return dtos;
    }

    public async Task<bool> ReviewAccessAsync(Guid accessId, Guid reviewerId, ReviewBreakGlassRequest request)
    {
        // Check if user is admin
        if (!await IsAdminAsync(reviewerId))
            return false;

        var access = await _context.BreakGlassAccesses.FindAsync(accessId);
        if (access == null)
            return false;

        if (access.Status != "under_review")
            return false;

        access.Status = "reviewed";
        access.ReviewedBy = reviewerId;
        access.ReviewedAt = DateTime.UtcNow;
        access.ReviewNotes = request.ReviewNotes;
        access.ReviewDecision = request.ReviewDecision;

        await _context.SaveChangesAsync();

        // Audit log
        await _auditService.LogAsync(
            reviewerId,
            "break_glass_reviewed",
            "BreakGlassAccess",
            accessId.ToString(),
            null,
            new
            {
                reviewDecision = request.ReviewDecision,
                originalUserId = access.UserId
            });

        // Notify user of review decision
        var user = await _context.Users.FindAsync(access.UserId);
        if (user?.Email != null)
        {
            await _emailService.SendSecurityAlertAsync(
                user.Email,
                $"Your break-glass access (ID: {accessId}) has been reviewed. Decision: {request.ReviewDecision}. Notes: {request.ReviewNotes}",
                user.UserName ?? user.Email);
        }

        _logger.LogInformation(
            "Break-glass access reviewed: {AccessId} by {ReviewerId}, Decision: {Decision}",
            accessId,
            reviewerId,
            request.ReviewDecision);

        return true;
    }

    public async Task<int> ProcessExpiredAccessesAsync()
    {
        var now = DateTime.UtcNow;

        var expiredAccesses = await _context.BreakGlassAccesses
            .Where(b => b.Status == "active" && b.ExpiresAt <= now)
            .ToListAsync();

        if (expiredAccesses.Count == 0)
            return 0;

        foreach (var access in expiredAccesses)
        {
            access.Status = access.RequiresReview ? "under_review" : "expired";
            access.DeactivatedAt = DateTime.UtcNow;

            // Notify user
            var user = await _context.Users.FindAsync(access.UserId);
            if (user?.Email != null)
            {
                await _emailService.SendSecurityAlertAsync(
                    user.Email,
                    $"Your break-glass access has expired. Please deactivate and provide details of accessed resources and actions performed.",
                    user.UserName ?? user.Email);
            }

            _logger.LogWarning(
                "Break-glass access expired: {AccessId}, User: {UserId}",
                access.Id,
                access.UserId);
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Processed {Count} expired break-glass accesses",
            expiredAccesses.Count);

        return expiredAccesses.Count;
    }

    public async Task<BreakGlassStatisticsDto> GetStatisticsAsync(Guid userId)
    {
        var isAdmin = await IsAdminAsync(userId);

        var query = _context.BreakGlassAccesses.AsQueryable();
        if (!isAdmin)
        {
            query = query.Where(b => b.UserId == userId);
        }

        var accesses = await query
            .Include(b => b.User)
            .ToListAsync();

        var now = DateTime.UtcNow;
        var totalActivations = accesses.Count;
        var activeNow = accesses.Count(b => b.Status == "active");
        var deactivatedCount = accesses.Count(b => b.Status == "deactivated");
        var expiredCount = accesses.Count(b => b.Status == "expired");
        var pendingReview = accesses.Count(b => b.Status == "under_review");
        var reviewedCount = accesses.Count(b => b.Status == "reviewed");
        var justifiedCount = accesses.Count(b => b.ReviewDecision == "justified");
        var unjustifiedCount = accesses.Count(b => b.ReviewDecision == "unjustified");
        var activationsLast24Hours = accesses.Count(b => b.ActivatedAt >= now.AddHours(-24));
        var activationsLast7Days = accesses.Count(b => b.ActivatedAt >= now.AddDays(-7));
        var activationsLast30Days = accesses.Count(b => b.ActivatedAt >= now.AddDays(-30));

        var completedAccesses = accesses.Where(b => b.DeactivatedAt.HasValue).ToList();
        var avgDuration = completedAccesses.Any()
            ? TimeSpan.FromMinutes(completedAccesses.Average(b =>
                (b.DeactivatedAt!.Value - b.ActivatedAt).TotalMinutes))
            : TimeSpan.Zero;

        var byIncidentType = accesses
            .GroupBy(b => b.IncidentType)
            .Select(g => new BreakGlassByIncidentTypeDto
            {
                IncidentType = g.Key,
                Count = g.Count(),
                JustifiedCount = g.Count(b => b.ReviewDecision == "justified")
            })
            .ToList();

        var bySeverity = accesses
            .GroupBy(b => b.Severity)
            .Select(g => new BreakGlassBySeverityDto
            {
                Severity = g.Key,
                Count = g.Count()
            })
            .ToList();

        var topUsers = accesses
            .GroupBy(b => new { b.UserId, b.User.Email })
            .Select(g => new BreakGlassByUserDto
            {
                UserId = g.Key.UserId,
                UserEmail = g.Key.Email ?? string.Empty,
                ActivationCount = g.Count(),
                JustifiedCount = g.Count(b => b.ReviewDecision == "justified"),
                UnjustifiedCount = g.Count(b => b.ReviewDecision == "unjustified")
            })
            .OrderByDescending(u => u.ActivationCount)
            .Take(10)
            .ToList();

        return new BreakGlassStatisticsDto
        {
            TotalActivations = totalActivations,
            ActiveNow = activeNow,
            DeactivatedCount = deactivatedCount,
            ExpiredCount = expiredCount,
            PendingReview = pendingReview,
            ReviewedCount = reviewedCount,
            JustifiedCount = justifiedCount,
            UnjustifiedCount = unjustifiedCount,
            ActivationsLast24Hours = activationsLast24Hours,
            ActivationsLast7Days = activationsLast7Days,
            ActivationsLast30Days = activationsLast30Days,
            AverageDuration = avgDuration,
            ByIncidentType = byIncidentType,
            BySeverity = bySeverity,
            TopUsersByActivations = topUsers
        };
    }

    // Policy Management

    public async Task<BreakGlassPolicyDto?> GetActivePolicyAsync()
    {
        var policy = await _context.BreakGlassPolicies
            .FirstOrDefaultAsync(p => p.Enabled);

        if (policy == null)
            return null;

        return await MapPolicyToDto(policy);
    }

    public async Task<BreakGlassPolicyDto> CreatePolicyAsync(Guid userId, CreateBreakGlassPolicyRequest request)
    {
        // Check if user is admin
        if (!await IsAdminAsync(userId))
            throw new InvalidOperationException("Only administrators can create break-glass policies");

        var policy = new BreakGlassPolicy
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            Enabled = request.Enabled,
            DefaultDurationMinutes = request.DefaultDurationMinutes,
            MaxDurationMinutes = request.MaxDurationMinutes,
            RequireJustification = request.RequireJustification,
            MinJustificationLength = request.MinJustificationLength,
            AutoNotifyExecutives = request.AutoNotifyExecutives,
            ExecutiveUserIds = request.ExecutiveUserIds != null && request.ExecutiveUserIds.Any()
                ? JsonSerializer.Serialize(request.ExecutiveUserIds) : null,
            MandatorySessionRecording = request.MandatorySessionRecording,
            RequirePostAccessReview = request.RequirePostAccessReview,
            ReviewRequiredWithinHours = request.ReviewRequiredWithinHours,
            AllowedIncidentTypes = request.AllowedIncidentTypes != null && request.AllowedIncidentTypes.Any()
                ? JsonSerializer.Serialize(request.AllowedIncidentTypes) : null,
            RestrictedToRoles = request.RestrictedToRoles != null && request.RestrictedToRoles.Any()
                ? JsonSerializer.Serialize(request.RestrictedToRoles) : null,
            NotificationChannels = request.NotificationChannels != null && request.NotificationChannels.Any()
                ? JsonSerializer.Serialize(request.NotificationChannels) : null,
            CreatedAt = DateTime.UtcNow
        };

        _context.BreakGlassPolicies.Add(policy);
        await _context.SaveChangesAsync();

        // Audit log
        await _auditService.LogAsync(
            userId,
            "break_glass_policy_created",
            "BreakGlassPolicy",
            policy.Id.ToString(),
            null,
            new { name = request.Name });

        _logger.LogInformation(
            "Break-glass policy created: {PolicyId} by user {UserId}",
            policy.Id,
            userId);

        return await MapPolicyToDto(policy);
    }

    public async Task<bool> UpdatePolicyAsync(Guid policyId, Guid userId, CreateBreakGlassPolicyRequest request)
    {
        if (!await IsAdminAsync(userId))
            return false;

        var policy = await _context.BreakGlassPolicies.FindAsync(policyId);
        if (policy == null)
            return false;

        policy.Name = request.Name;
        policy.Description = request.Description;
        policy.Enabled = request.Enabled;
        policy.DefaultDurationMinutes = request.DefaultDurationMinutes;
        policy.MaxDurationMinutes = request.MaxDurationMinutes;
        policy.RequireJustification = request.RequireJustification;
        policy.MinJustificationLength = request.MinJustificationLength;
        policy.AutoNotifyExecutives = request.AutoNotifyExecutives;
        policy.ExecutiveUserIds = request.ExecutiveUserIds != null && request.ExecutiveUserIds.Any()
            ? JsonSerializer.Serialize(request.ExecutiveUserIds) : null;
        policy.MandatorySessionRecording = request.MandatorySessionRecording;
        policy.RequirePostAccessReview = request.RequirePostAccessReview;
        policy.ReviewRequiredWithinHours = request.ReviewRequiredWithinHours;
        policy.AllowedIncidentTypes = request.AllowedIncidentTypes != null && request.AllowedIncidentTypes.Any()
            ? JsonSerializer.Serialize(request.AllowedIncidentTypes) : null;
        policy.RestrictedToRoles = request.RestrictedToRoles != null && request.RestrictedToRoles.Any()
            ? JsonSerializer.Serialize(request.RestrictedToRoles) : null;
        policy.NotificationChannels = request.NotificationChannels != null && request.NotificationChannels.Any()
            ? JsonSerializer.Serialize(request.NotificationChannels) : null;
        policy.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Audit log
        await _auditService.LogAsync(
            userId,
            "break_glass_policy_updated",
            "BreakGlassPolicy",
            policyId.ToString(),
            null,
            new { name = request.Name });

        return true;
    }

    public async Task<List<BreakGlassPolicyDto>> GetPoliciesAsync(Guid userId)
    {
        if (!await IsAdminAsync(userId))
            return new List<BreakGlassPolicyDto>();

        var policies = await _context.BreakGlassPolicies
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        var dtos = new List<BreakGlassPolicyDto>();
        foreach (var policy in policies)
        {
            dtos.Add(await MapPolicyToDto(policy));
        }

        return dtos;
    }

    // Private helper methods

    private async Task<BreakGlassAccessDto> MapToDto(BreakGlassAccess access)
    {
        TimeSpan? remainingTime = null;
        if (access.Status == "active")
        {
            var remaining = access.ExpiresAt - DateTime.UtcNow;
            remainingTime = remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }

        TimeSpan? duration = null;
        if (access.DeactivatedAt.HasValue)
        {
            duration = access.DeactivatedAt.Value - access.ActivatedAt;
        }

        var accessedResources = !string.IsNullOrEmpty(access.AccessedResources)
            ? JsonSerializer.Deserialize<List<string>>(access.AccessedResources)
            : null;

        var actionsPerformed = !string.IsNullOrEmpty(access.ActionsPerformed)
            ? JsonSerializer.Deserialize<List<string>>(access.ActionsPerformed)
            : null;

        var notifiedExecutiveEmails = new List<string>();
        if (!string.IsNullOrEmpty(access.NotifiedExecutives))
        {
            var executiveIds = JsonSerializer.Deserialize<List<Guid>>(access.NotifiedExecutives) ?? new List<Guid>();
            notifiedExecutiveEmails = await _context.Users
                .Where(u => executiveIds.Contains(u.Id))
                .Select(u => u.Email!)
                .ToListAsync();
        }

        return new BreakGlassAccessDto
        {
            Id = access.Id,
            UserId = access.UserId,
            UserEmail = access.User?.Email ?? string.Empty,
            Reason = access.Reason,
            IncidentType = access.IncidentType,
            Severity = access.Severity,
            ActivatedAt = access.ActivatedAt,
            DeactivatedAt = access.DeactivatedAt,
            ExpiresAt = access.ExpiresAt,
            Status = access.Status,
            DurationMinutes = access.DurationMinutes,
            SessionRecordingMandatory = access.SessionRecordingMandatory,
            SessionId = access.SessionId,
            AccessedResources = accessedResources,
            ActionsPerformed = actionsPerformed,
            ExecutiveNotified = access.ExecutiveNotified,
            ExecutiveNotifiedAt = access.ExecutiveNotifiedAt,
            NotifiedExecutiveEmails = notifiedExecutiveEmails,
            RequiresReview = access.RequiresReview,
            ReviewedBy = access.ReviewedBy,
            ReviewedByEmail = access.Reviewer?.Email,
            ReviewedAt = access.ReviewedAt,
            ReviewNotes = access.ReviewNotes,
            ReviewDecision = access.ReviewDecision,
            IpAddress = access.IpAddress,
            Location = access.Location,
            RemainingTime = remainingTime,
            Duration = duration
        };
    }

    private async Task<BreakGlassPolicyDto> MapPolicyToDto(BreakGlassPolicy policy)
    {
        var executiveEmails = new List<string>();
        if (!string.IsNullOrEmpty(policy.ExecutiveUserIds))
        {
            var executiveIds = JsonSerializer.Deserialize<List<Guid>>(policy.ExecutiveUserIds) ?? new List<Guid>();
            executiveEmails = await _context.Users
                .Where(u => executiveIds.Contains(u.Id))
                .Select(u => u.Email!)
                .ToListAsync();
        }

        var restrictedToRoleNames = new List<string>();
        if (!string.IsNullOrEmpty(policy.RestrictedToRoles))
        {
            var roleIds = JsonSerializer.Deserialize<List<Guid>>(policy.RestrictedToRoles) ?? new List<Guid>();
            restrictedToRoleNames = await _context.Roles
                .Where(r => roleIds.Contains(r.Id))
                .Select(r => r.Name!)
                .ToListAsync();
        }

        return new BreakGlassPolicyDto
        {
            Id = policy.Id,
            Name = policy.Name,
            Description = policy.Description,
            Enabled = policy.Enabled,
            DefaultDurationMinutes = policy.DefaultDurationMinutes,
            MaxDurationMinutes = policy.MaxDurationMinutes,
            RequireJustification = policy.RequireJustification,
            MinJustificationLength = policy.MinJustificationLength,
            AutoNotifyExecutives = policy.AutoNotifyExecutives,
            ExecutiveEmails = executiveEmails,
            MandatorySessionRecording = policy.MandatorySessionRecording,
            RequirePostAccessReview = policy.RequirePostAccessReview,
            ReviewRequiredWithinHours = policy.ReviewRequiredWithinHours,
            AllowedIncidentTypes = !string.IsNullOrEmpty(policy.AllowedIncidentTypes)
                ? JsonSerializer.Deserialize<List<string>>(policy.AllowedIncidentTypes)
                : null,
            RestrictedToRoleNames = restrictedToRoleNames,
            NotificationChannels = !string.IsNullOrEmpty(policy.NotificationChannels)
                ? JsonSerializer.Deserialize<List<string>>(policy.NotificationChannels)
                : null,
            CreatedAt = policy.CreatedAt,
            UpdatedAt = policy.UpdatedAt
        };
    }

    private async Task<bool> IsAdminAsync(Guid userId)
    {
        var adminRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Admin");
        if (adminRole == null)
            return false;

        return await _context.UserRoles.AnyAsync(ur => ur.UserId == userId && ur.RoleId == adminRole.Id);
    }

    private async Task NotifyExecutivesAsync(List<Guid> executiveIds, BreakGlassAccess access)
    {
        var user = await _context.Users.FindAsync(access.UserId);

        foreach (var executiveId in executiveIds)
        {
            var executive = await _context.Users.FindAsync(executiveId);
            if (executive?.Email != null)
            {
                await _emailService.SendSecurityAlertAsync(
                    executive.Email,
                    $"CRITICAL: Break-glass emergency access activated by {user?.Email ?? "Unknown User"}. " +
                    $"Incident Type: {access.IncidentType}, Severity: {access.Severity}, " +
                    $"Reason: {access.Reason}, Duration: {access.DurationMinutes} minutes.",
                    executive.UserName ?? executive.Email);
            }
        }

        _logger.LogCritical(
            "EXECUTIVE NOTIFICATION: Break-glass access activated by {UserId}, notified {Count} executives",
            access.UserId,
            executiveIds.Count);
    }
}
