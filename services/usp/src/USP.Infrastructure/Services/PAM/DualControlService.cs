using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using USP.Core.Models.DTOs.PAM;
using USP.Core.Models.Entities;
using USP.Core.Services.Audit;
using USP.Core.Services.Communication;
using USP.Core.Services.PAM;
using USP.Core.Services.Webhook;
using USP.Infrastructure.Data;

namespace USP.Infrastructure.Services.PAM;

public class DualControlService : IDualControlService
{
    private readonly ApplicationDbContext _context;
    private readonly IAuditService _auditService;
    private readonly IEmailService _emailService;
    private readonly IWebhookService _webhookService;
    private readonly ILogger<DualControlService> _logger;

    public DualControlService(
        ApplicationDbContext context,
        IAuditService auditService,
        IEmailService emailService,
        IWebhookService webhookService,
        ILogger<DualControlService> logger)
    {
        _context = context;
        _auditService = auditService;
        _emailService = emailService;
        _webhookService = webhookService;
        _logger = logger;
    }

    public async Task<AccessApprovalDto> CreateApprovalRequestAsync(CreateApprovalRequest request, Guid requesterId)
    {
        // Validation
        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new ArgumentException("Reason is required", nameof(request));

        if (request.Approvers == null || request.Approvers.Count == 0)
            throw new ArgumentException("At least one approver is required", nameof(request));

        // Validate approval policy
        var validPolicies = new[] { "single_approver", "dual_control", "all_approvers", "majority" };
        if (!validPolicies.Contains(request.ApprovalPolicy))
            throw new ArgumentException($"Invalid approval policy. Valid values: {string.Join(", ", validPolicies)}", nameof(request));

        // Calculate required approvals
        var requiredApprovals = request.ApprovalPolicy switch
        {
            "single_approver" => 1,
            "dual_control" => 2,
            "all_approvers" => request.Approvers.Count,
            "majority" => (int)Math.Ceiling(request.Approvers.Count / 2.0),
            _ => 1
        };

        // Create approval
        var approval = new AccessApproval
        {
            Id = Guid.NewGuid(),
            RequesterId = requesterId,
            ResourceType = request.ResourceType,
            ResourceId = request.ResourceId,
            Reason = request.Reason,
            Status = "pending",
            ApprovalPolicy = request.ApprovalPolicy,
            RequiredApprovals = requiredApprovals,
            CurrentApprovals = 0,
            RequestedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(request.ExpirationHours),
            Approvers = request.Approvers,
            ApprovedBy = new List<Guid>()
        };

        _context.AccessApprovals.Add(approval);
        await _context.SaveChangesAsync();

        // Audit log
        await _auditService.LogAsync(
            requesterId,
            "approval_request_created",
            request.ResourceType,
            request.ResourceId.ToString(),
            null,
            new
            {
                approvalId = approval.Id,
                approvers = request.Approvers,
                policy = request.ApprovalPolicy,
                expiresAt = approval.ExpiresAt
            });

        // Send notification to approvers
        await NotifyApproversAsync(approval);

        _logger.LogInformation(
            "Approval request {ApprovalId} created by user {RequesterId} for {ResourceType} {ResourceId}",
            approval.Id,
            requesterId,
            request.ResourceType,
            request.ResourceId);

        return await MapToDtoAsync(approval);
    }

    public async Task<bool> ApproveAsync(Guid approvalId, Guid approverId, string? notes = null)
    {
        var approval = await _context.AccessApprovals
            .FirstOrDefaultAsync(a => a.Id == approvalId);

        if (approval == null)
            return false;

        // Check if user is an approver
        if (!approval.Approvers.Contains(approverId))
            throw new UnauthorizedAccessException("User is not an approver for this request");

        // Check if approval is still pending
        if (approval.Status != "pending")
            throw new InvalidOperationException($"Approval is not pending (status: {approval.Status})");

        // Check if expired
        if (DateTime.UtcNow > approval.ExpiresAt)
        {
            approval.Status = "expired";
            await _context.SaveChangesAsync();
            throw new InvalidOperationException("Approval request has expired");
        }

        // Check if already approved by this user
        if (approval.ApprovedBy.Contains(approverId))
            throw new InvalidOperationException("You have already approved this request");

        // Record approval
        approval.ApprovedBy.Add(approverId);
        approval.CurrentApprovals = approval.ApprovedBy.Count;

        // Check if approval is complete based on policy
        if (approval.CurrentApprovals >= approval.RequiredApprovals)
        {
            approval.Status = "approved";
            approval.ApprovedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        // Audit log
        await _auditService.LogAsync(
            approverId,
            "approval_granted",
            approval.ResourceType,
            approval.ResourceId.ToString(),
            null,
            new
            {
                approvalId,
                notes,
                isComplete = approval.Status == "approved",
                policy = approval.ApprovalPolicy
            });

        _logger.LogInformation(
            "Approval {ApprovalId} approved by user {ApproverId}. Complete: {IsComplete}",
            approvalId,
            approverId,
            approval.Status == "approved");

        // Send webhook notification if complete
        if (approval.Status == "approved")
        {
            await _webhookService.PublishEventAsync("approval.completed", new
            {
                approvalId,
                resourceType = approval.ResourceType,
                resourceId = approval.ResourceId,
                status = "approved"
            });

            // Notify requester
            await NotifyRequesterAsync(approval, "approved");
        }

        return true;
    }

    public async Task<bool> DenyAsync(Guid approvalId, Guid approverId, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Reason is required for denial", nameof(reason));

        var approval = await _context.AccessApprovals
            .FirstOrDefaultAsync(a => a.Id == approvalId);

        if (approval == null)
            return false;

        // Check if user is an approver
        if (!approval.Approvers.Contains(approverId))
            throw new UnauthorizedAccessException("User is not an approver for this request");

        // Check if approval is still pending
        if (approval.Status != "pending")
            throw new InvalidOperationException($"Approval is not pending (status: {approval.Status})");

        // Record denial
        approval.DeniedBy = approverId.ToString();
        approval.DenialReason = reason;
        approval.Status = "denied";
        approval.DeniedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Audit log
        await _auditService.LogAsync(
            approverId,
            "approval_denied",
            approval.ResourceType,
            approval.ResourceId.ToString(),
            null,
            new
            {
                approvalId,
                reason,
                policy = approval.ApprovalPolicy
            });

        _logger.LogWarning(
            "Approval {ApprovalId} denied by user {ApproverId}. Reason: {Reason}",
            approvalId,
            approverId,
            reason);

        // Send webhook notification
        await _webhookService.PublishEventAsync("approval.completed", new
        {
            approvalId,
            resourceType = approval.ResourceType,
            resourceId = approval.ResourceId,
            status = "denied"
        });

        // Notify requester
        await NotifyRequesterAsync(approval, "denied");

        return true;
    }

    public async Task<List<AccessApprovalDto>> GetPendingApprovalsAsync(Guid userId)
    {
        var approvals = await _context.AccessApprovals
            .Where(a => a.Approvers.Contains(userId) && a.Status == "pending")
            .OrderByDescending(a => a.RequestedAt)
            .ToListAsync();

        var dtos = new List<AccessApprovalDto>();
        foreach (var approval in approvals)
        {
            dtos.Add(await MapToDtoAsync(approval));
        }

        return dtos;
    }

    public async Task<List<AccessApprovalDto>> GetMyRequestsAsync(Guid userId)
    {
        var approvals = await _context.AccessApprovals
            .Where(a => a.RequesterId == userId)
            .OrderByDescending(a => a.RequestedAt)
            .ToListAsync();

        var dtos = new List<AccessApprovalDto>();
        foreach (var approval in approvals)
        {
            dtos.Add(await MapToDtoAsync(approval));
        }

        return dtos;
    }

    public async Task<AccessApprovalDto?> GetApprovalByIdAsync(Guid approvalId, Guid userId)
    {
        var approval = await _context.AccessApprovals
            .FirstOrDefaultAsync(a => a.Id == approvalId);

        if (approval == null)
            return null;

        // Only requester or approvers can view
        if (approval.RequesterId != userId && !approval.Approvers.Contains(userId))
            return null;

        return await MapToDtoAsync(approval);
    }

    public async Task<bool> IsApprovalCompleteAsync(Guid approvalId)
    {
        var approval = await _context.AccessApprovals.FindAsync(approvalId);
        return approval != null && (approval.Status == "approved" || approval.Status == "denied");
    }

    public async Task<bool> CancelApprovalAsync(Guid approvalId, Guid userId)
    {
        var approval = await _context.AccessApprovals
            .FirstOrDefaultAsync(a => a.Id == approvalId);

        if (approval == null)
            return false;

        // Only requester can cancel
        if (approval.RequesterId != userId)
            throw new UnauthorizedAccessException("Only the requester can cancel the approval");

        // Can only cancel pending approvals
        if (approval.Status != "pending")
            throw new InvalidOperationException($"Cannot cancel approval with status: {approval.Status}");

        approval.Status = "cancelled";

        await _context.SaveChangesAsync();

        // Audit log
        await _auditService.LogAsync(
            userId,
            "approval_cancelled",
            approval.ResourceType,
            approval.ResourceId.ToString(),
            null,
            new { approvalId });

        _logger.LogInformation(
            "Approval {ApprovalId} cancelled by requester {UserId}",
            approvalId,
            userId);

        return true;
    }

    public async Task<int> ProcessExpiredApprovalsAsync()
    {
        var expiredApprovals = await _context.AccessApprovals
            .Where(a => a.Status == "pending" && a.ExpiresAt < DateTime.UtcNow)
            .ToListAsync();

        if (expiredApprovals.Count == 0)
            return 0;

        foreach (var approval in expiredApprovals)
        {
            approval.Status = "expired";

            // Audit log
            await _auditService.LogAsync(
                approval.RequesterId,
                "approval_expired",
                approval.ResourceType,
                approval.ResourceId.ToString(),
                null,
                new
                {
                    approvalId = approval.Id,
                    expiresAt = approval.ExpiresAt,
                    autoExpiredAt = DateTime.UtcNow
                });

            _logger.LogInformation(
                "Approval {ApprovalId} auto-expired",
                approval.Id);
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Processed {Count} expired approvals",
            expiredApprovals.Count);

        return expiredApprovals.Count;
    }

    public async Task<ApprovalStatisticsDto> GetApprovalStatisticsAsync(Guid userId)
    {
        var now = DateTime.UtcNow;
        var last24Hours = now.AddHours(-24);
        var last7Days = now.AddDays(-7);
        var last30Days = now.AddDays(-30);

        var allApprovals = await _context.AccessApprovals
            .Where(a => a.RequesterId == userId)
            .ToListAsync();

        var pendingCount = allApprovals.Count(a => a.Status == "pending");
        var approvedCount = allApprovals.Count(a => a.Status == "approved");
        var deniedCount = allApprovals.Count(a => a.Status == "denied");
        var expiredCount = allApprovals.Count(a => a.Status == "expired");

        var approvalsLast24Hours = allApprovals.Count(a => a.RequestedAt >= last24Hours);
        var approvalsLast7Days = allApprovals.Count(a => a.RequestedAt >= last7Days);
        var approvalsLast30Days = allApprovals.Count(a => a.RequestedAt >= last30Days);

        var completedApprovals = allApprovals
            .Where(a => a.ApprovedAt.HasValue)
            .ToList();

        var averageApprovalTime = completedApprovals.Any()
            ? completedApprovals.Average(a => (a.ApprovedAt!.Value - a.RequestedAt).TotalMinutes)
            : 0;

        var recentApprovals = allApprovals
            .OrderByDescending(a => a.RequestedAt)
            .Take(10)
            .Select(a => new ApprovalSummaryDto
            {
                ApprovalId = a.Id,
                ResourceType = a.ResourceType,
                ResourceName = GetResourceName(a.ResourceType, a.ResourceId),
                RequesterEmail = string.Empty,
                RequestedAt = a.RequestedAt,
                CompletedAt = a.ApprovedAt ?? a.DeniedAt,
                Status = a.Status,
                ApprovalPolicy = a.ApprovalPolicy
            })
            .ToList();

        return new ApprovalStatisticsDto
        {
            PendingApprovals = pendingCount,
            TotalApprovals = allApprovals.Count,
            ApprovalsLast24Hours = approvalsLast24Hours,
            ApprovalsLast7Days = approvalsLast7Days,
            ApprovalsLast30Days = approvalsLast30Days,
            ApprovedCount = approvedCount,
            DeniedCount = deniedCount,
            ExpiredCount = expiredCount,
            AverageApprovalTimeMinutes = averageApprovalTime,
            RecentApprovals = recentApprovals
        };
    }

    private async Task<AccessApprovalDto> MapToDtoAsync(AccessApproval approval)
    {
        var requester = await _context.Users.FindAsync(approval.RequesterId);

        var approverActions = new List<ApproverActionDto>();

        // Add approved actions
        foreach (var approverId in approval.ApprovedBy)
        {
            var approver = await _context.Users.FindAsync(approverId);
            approverActions.Add(new ApproverActionDto
            {
                ApproverId = approverId,
                ApproverEmail = approver?.Email ?? string.Empty,
                Action = "approved",
                ActionAt = approval.ApprovedAt,
                Notes = null
            });
        }

        // Add denied action if applicable
        if (!string.IsNullOrEmpty(approval.DeniedBy))
        {
            var deniedById = Guid.Parse(approval.DeniedBy);
            var denier = await _context.Users.FindAsync(deniedById);
            approverActions.Add(new ApproverActionDto
            {
                ApproverId = deniedById,
                ApproverEmail = denier?.Email ?? string.Empty,
                Action = "denied",
                ActionAt = approval.DeniedAt,
                Notes = approval.DenialReason
            });
        }

        // Add pending approvers
        foreach (var approverId in approval.Approvers)
        {
            if (!approval.ApprovedBy.Contains(approverId) &&
                approval.DeniedBy != approverId.ToString())
            {
                var approver = await _context.Users.FindAsync(approverId);
                approverActions.Add(new ApproverActionDto
                {
                    ApproverId = approverId,
                    ApproverEmail = approver?.Email ?? string.Empty,
                    Action = "pending",
                    ActionAt = null,
                    Notes = null
                });
            }
        }

        return new AccessApprovalDto
        {
            Id = approval.Id,
            RequesterId = approval.RequesterId,
            RequesterEmail = requester?.Email ?? string.Empty,
            ResourceType = approval.ResourceType,
            ResourceId = approval.ResourceId,
            ResourceName = GetResourceName(approval.ResourceType, approval.ResourceId),
            Reason = approval.Reason,
            Status = approval.Status,
            ApprovalPolicy = approval.ApprovalPolicy,
            RequestedAt = approval.RequestedAt,
            ExpiresAt = approval.ExpiresAt,
            CompletedAt = approval.ApprovedAt ?? approval.DeniedAt,
            ApproverActions = approverActions,
            RequiredApprovals = approval.RequiredApprovals,
            CurrentApprovals = approval.CurrentApprovals
        };
    }

    private async Task NotifyApproversAsync(AccessApproval approval)
    {
        foreach (var approverId in approval.Approvers)
        {
            var approver = await _context.Users.FindAsync(approverId);
            if (approver?.Email == null)
                continue;

            var requester = await _context.Users.FindAsync(approval.RequesterId);

            try
            {
                await _emailService.SendSecurityAlertAsync(
                    approver.Email,
                    $"Approval request from {requester?.Email ?? "Unknown"} for {approval.ResourceType}. Reason: {approval.Reason}. Expires: {approval.ExpiresAt:yyyy-MM-dd HH:mm:ss} UTC",
                    approver.UserName ?? approver.Email);

                _logger.LogDebug("Approval notification sent to {Email}", approver.Email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send approval notification to {Email}", approver.Email);
            }
        }

        // Publish webhook event
        await _webhookService.PublishEventAsync("approval.requested", new
        {
            approvalId = approval.Id,
            resourceType = approval.ResourceType,
            resourceId = approval.ResourceId,
            approvers = approval.Approvers
        });
    }

    private async Task NotifyRequesterAsync(AccessApproval approval, string decision)
    {
        var requester = await _context.Users.FindAsync(approval.RequesterId);
        if (requester?.Email == null)
            return;

        try
        {
            await _emailService.SendSecurityAlertAsync(
                requester.Email,
                $"Your approval request has been {decision}. Resource: {approval.ResourceType}. Status: {approval.Status}",
                requester.UserName ?? requester.Email);

            _logger.LogDebug("Approval decision notification sent to requester {Email}", requester.Email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send decision notification to requester {Email}", requester.Email);
        }
    }

    private string GetResourceName(string resourceType, Guid resourceId)
    {
        // Resource name lookup based on type
        // Could be enhanced with a resource lookup service or caching layer
        return resourceType switch
        {
            "Safe" => $"Safe-{resourceId}",
            "PrivilegedAccount" => $"Account-{resourceId}",
            "Secret" => $"Secret-{resourceId}",
            "PkiRole" => $"PKI Role-{resourceId}",
            _ => resourceId.ToString()
        };
    }
}
