using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using USP.Core.Models.DTOs.Compliance;
using USP.Core.Models.Entities;
using USP.Core.Services.Compliance;
using USP.Infrastructure.Data;

namespace USP.Infrastructure.Services.Compliance;

/// <summary>
/// Access certification and recertification service implementation
/// </summary>
public class AccessCertificationService : IAccessCertificationService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AccessCertificationService> _logger;

    public AccessCertificationService(
        ApplicationDbContext context,
        ILogger<AccessCertificationService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<CertificationCampaignDto> CreateCampaignAsync(CreateCertificationCampaignRequest request, Guid initiatedBy)
    {
        _logger.LogInformation("Creating certification campaign: {CampaignName}", request.Name);

        try
        {
            var campaign = new AccessCertificationCampaign
            {
                Id = Guid.NewGuid(),
                Name = request.Name,
                Description = request.Description,
                CampaignType = request.CampaignType,
                Status = "Draft",
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                AutoRevokeOnExpiry = request.AutoRevokeOnExpiry,
                ReminderDaysBeforeDeadline = request.ReminderDaysBeforeDeadline,
                InitiatedBy = initiatedBy,
                CreatedAt = DateTime.UtcNow
            };

            _context.AccessCertificationCampaigns.Add(campaign);
            await _context.SaveChangesAsync();

            await GenerateReviewsForCampaignAsync(campaign, request.TargetRoles, request.TargetUserIds);

            var stats = await GetCampaignStatisticsAsync(campaign.Id);

            _logger.LogInformation("Certification campaign created: {CampaignId}, Total Reviews: {TotalReviews}", campaign.Id, stats.TotalReviews);

            return await MapCampaignToDtoAsync(campaign);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create certification campaign: {CampaignName}", request.Name);
            throw;
        }
    }

    public async Task<CertificationCampaignDto?> GetCampaignAsync(Guid campaignId)
    {
        var campaign = await _context.AccessCertificationCampaigns
            .FirstOrDefaultAsync(c => c.Id == campaignId);

        if (campaign == null)
        {
            return null;
        }

        return await MapCampaignToDtoAsync(campaign);
    }

    public async Task<List<CertificationCampaignDto>> ListCampaignsAsync(string? status = null)
    {
        var query = _context.AccessCertificationCampaigns.AsQueryable();

        if (!string.IsNullOrEmpty(status))
        {
            query = query.Where(c => c.Status == status);
        }

        var campaigns = await query
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();

        var result = new List<CertificationCampaignDto>();
        foreach (var campaign in campaigns)
        {
            result.Add(await MapCampaignToDtoAsync(campaign));
        }

        return result;
    }

    public async Task StartCampaignAsync(Guid campaignId, Guid startedBy)
    {
        _logger.LogInformation("Starting certification campaign: {CampaignId}", campaignId);

        var campaign = await _context.AccessCertificationCampaigns
            .FirstOrDefaultAsync(c => c.Id == campaignId);

        if (campaign == null)
        {
            throw new InvalidOperationException($"Campaign {campaignId} not found");
        }

        if (campaign.Status != "Draft")
        {
            throw new InvalidOperationException($"Campaign must be in Draft status to start. Current status: {campaign.Status}");
        }

        campaign.Status = "Active";
        campaign.StartDate = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Certification campaign started: {CampaignId}", campaignId);
    }

    public async Task CloseCampaignAsync(Guid campaignId, Guid closedBy)
    {
        _logger.LogInformation("Closing certification campaign: {CampaignId}", campaignId);

        var campaign = await _context.AccessCertificationCampaigns
            .FirstOrDefaultAsync(c => c.Id == campaignId);

        if (campaign == null)
        {
            throw new InvalidOperationException($"Campaign {campaignId} not found");
        }

        campaign.Status = "Completed";
        campaign.CompletedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Certification campaign closed: {CampaignId}", campaignId);
    }

    public async Task<List<AccessReviewDto>> GetPendingReviewsAsync(Guid reviewerId)
    {
        var reviews = await _context.AccessCertificationReviews
            .Include(r => r.Campaign)
            .Where(r => r.ReviewerId == reviewerId && r.Status == "Pending")
            .OrderBy(r => r.DueDate)
            .ToListAsync();

        var result = new List<AccessReviewDto>();

        foreach (var review in reviews)
        {
            var user = await _context.Users.FindAsync(review.UserId);
            var reviewer = await _context.Users.FindAsync(review.ReviewerId);

            result.Add(new AccessReviewDto
            {
                Id = review.Id,
                CampaignId = review.CampaignId,
                CampaignName = review.Campaign.Name,
                UserId = review.UserId,
                Username = user?.UserName ?? "Unknown",
                UserEmail = user?.Email ?? "Unknown",
                AccessType = review.AccessType,
                AccessValue = review.AccessValue,
                ReviewerId = review.ReviewerId,
                ReviewerName = reviewer?.UserName ?? "Unknown",
                Status = review.Status,
                DueDate = review.DueDate,
                ReviewedAt = review.ReviewedAt,
                ReviewComment = review.ReviewComment
            });
        }

        return result;
    }

    public async Task CertifyAccessAsync(Guid reviewId, CertifyAccessRequest request, Guid reviewerId)
    {
        _logger.LogInformation("Certifying access. ReviewId: {ReviewId}, ReviewerId: {ReviewerId}", reviewId, reviewerId);

        var review = await _context.AccessCertificationReviews
            .FirstOrDefaultAsync(r => r.Id == reviewId);

        if (review == null)
        {
            throw new InvalidOperationException($"Review {reviewId} not found");
        }

        if (review.ReviewerId != reviewerId)
        {
            throw new UnauthorizedAccessException("You are not authorized to review this access");
        }

        if (review.Status != "Pending")
        {
            throw new InvalidOperationException($"Review is not in Pending status. Current status: {review.Status}");
        }

        review.Status = "Approved";
        review.ReviewedAt = DateTime.UtcNow;
        review.ReviewComment = request.Comment;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Access certified. ReviewId: {ReviewId}, User: {UserId}, Access: {AccessType}:{AccessValue}",
            reviewId, review.UserId, review.AccessType, review.AccessValue);
    }

    public async Task RevokeAccessAsync(Guid reviewId, RevokeAccessRequest request, Guid reviewerId)
    {
        _logger.LogInformation("Revoking access. ReviewId: {ReviewId}, ReviewerId: {ReviewerId}", reviewId, reviewerId);

        var review = await _context.AccessCertificationReviews
            .FirstOrDefaultAsync(r => r.Id == reviewId);

        if (review == null)
        {
            throw new InvalidOperationException($"Review {reviewId} not found");
        }

        if (review.ReviewerId != reviewerId)
        {
            throw new UnauthorizedAccessException("You are not authorized to review this access");
        }

        if (review.Status != "Pending")
        {
            throw new InvalidOperationException($"Review is not in Pending status. Current status: {review.Status}");
        }

        review.Status = "Revoked";
        review.ReviewedAt = DateTime.UtcNow;
        review.ReviewComment = request.Reason;

        await _context.SaveChangesAsync();

        if (request.RevokeImmediately)
        {
            await RevokeUserAccessAsync(review.UserId, review.AccessType, review.AccessValue);
        }

        _logger.LogInformation("Access revoked. ReviewId: {ReviewId}, User: {UserId}, Access: {AccessType}:{AccessValue}, Reason: {Reason}",
            reviewId, review.UserId, review.AccessType, review.AccessValue, request.Reason);
    }

    public async Task DelegateReviewAsync(Guid reviewId, Guid delegateToUserId, Guid delegatedBy)
    {
        _logger.LogInformation("Delegating review. ReviewId: {ReviewId}, DelegateTo: {DelegateToUserId}", reviewId, delegateToUserId);

        var review = await _context.AccessCertificationReviews
            .FirstOrDefaultAsync(r => r.Id == reviewId);

        if (review == null)
        {
            throw new InvalidOperationException($"Review {reviewId} not found");
        }

        if (review.ReviewerId != delegatedBy)
        {
            throw new UnauthorizedAccessException("You are not authorized to delegate this review");
        }

        review.ReviewerId = delegateToUserId;
        review.Status = "Delegated";
        review.DelegatedAt = DateTime.UtcNow;
        review.DelegatedBy = delegatedBy;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Review delegated. ReviewId: {ReviewId}, NewReviewer: {DelegateToUserId}", reviewId, delegateToUserId);
    }

    public async Task<List<OrphanedAccountDto>> DetectOrphanedAccountsAsync()
    {
        _logger.LogInformation("Detecting orphaned accounts");

        var orphanedAccounts = new List<OrphanedAccountDto>();

        var inactiveThresholdDate = DateTime.UtcNow.AddDays(-90);

        var users = await _context.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .Where(u => !u.LockoutEnabled || u.LastLoginAt < inactiveThresholdDate)
            .ToListAsync();

        foreach (var user in users)
        {
            var reason = string.Empty;
            var daysSinceLastLogin = 0;

            if (user.LastLoginAt == null)
            {
                reason = "NoLogin";
                daysSinceLastLogin = int.MaxValue;
            }
            else if (user.LastLoginAt < inactiveThresholdDate)
            {
                reason = "Inactive";
                daysSinceLastLogin = (DateTime.UtcNow - user.LastLoginAt.Value).Days;
            }

            if (!user.LockoutEnabled)
            {
                reason = reason == string.Empty ? "Suspended" : $"{reason},Suspended";
            }

            if (!string.IsNullOrEmpty(reason))
            {
                orphanedAccounts.Add(new OrphanedAccountDto
                {
                    UserId = user.Id,
                    Username = user.UserName ?? "Unknown",
                    Email = user.Email ?? "Unknown",
                    Reason = reason,
                    LastLoginAt = user.LastLoginAt,
                    DaysSinceLastLogin = daysSinceLastLogin,
                    AssignedRoles = user.UserRoles.Select(ur => ur.Role.Name).ToList()
                });
            }
        }

        _logger.LogInformation("Detected {Count} orphaned accounts", orphanedAccounts.Count);

        return orphanedAccounts;
    }

    public async Task<CampaignStatisticsDto> GetCampaignStatisticsAsync(Guid campaignId)
    {
        var reviews = await _context.AccessCertificationReviews
            .Where(r => r.CampaignId == campaignId)
            .ToListAsync();

        var totalReviews = reviews.Count;
        var pendingReviews = reviews.Count(r => r.Status == "Pending");
        var completedReviews = reviews.Count(r => r.Status == "Approved" || r.Status == "Revoked");
        var approvedCount = reviews.Count(r => r.Status == "Approved");
        var revokedCount = reviews.Count(r => r.Status == "Revoked");
        var delegatedCount = reviews.Count(r => r.Status == "Delegated");

        var completionPercentage = totalReviews > 0 ? (decimal)completedReviews / totalReviews * 100 : 0;

        var reviewerStats = reviews
            .GroupBy(r => r.ReviewerId)
            .ToDictionary(g => g.Key.ToString(), g => g.Count());

        var accessTypeBreakdown = reviews
            .GroupBy(r => r.AccessType)
            .ToDictionary(g => g.Key, g => g.Count());

        return new CampaignStatisticsDto
        {
            CampaignId = campaignId,
            TotalReviews = totalReviews,
            PendingReviews = pendingReviews,
            CompletedReviews = completedReviews,
            ApprovedCount = approvedCount,
            RevokedCount = revokedCount,
            DelegatedCount = delegatedCount,
            CompletionPercentage = completionPercentage,
            ReviewerStatistics = reviewerStats,
            AccessTypeBreakdown = accessTypeBreakdown
        };
    }

    public async Task ProcessExpiredReviewsAsync()
    {
        _logger.LogInformation("Processing expired reviews for auto-revocation");

        var expiredReviews = await _context.AccessCertificationReviews
            .Include(r => r.Campaign)
            .Where(r => r.Status == "Pending" &&
                       r.DueDate < DateTime.UtcNow &&
                       r.Campaign.AutoRevokeOnExpiry)
            .ToListAsync();

        foreach (var review in expiredReviews)
        {
            review.Status = "Revoked";
            review.ReviewedAt = DateTime.UtcNow;
            review.ReviewComment = "Automatically revoked due to expired review deadline";

            await RevokeUserAccessAsync(review.UserId, review.AccessType, review.AccessValue);

            _logger.LogWarning("Auto-revoked access. User: {UserId}, Access: {AccessType}:{AccessValue}",
                review.UserId, review.AccessType, review.AccessValue);
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Processed {Count} expired reviews", expiredReviews.Count);
    }

    public async Task SendReviewRemindersAsync(Guid campaignId)
    {
        _logger.LogInformation("Sending review reminders for campaign: {CampaignId}", campaignId);

        var campaign = await _context.AccessCertificationCampaigns
            .FirstOrDefaultAsync(c => c.Id == campaignId);

        if (campaign == null)
        {
            throw new InvalidOperationException($"Campaign {campaignId} not found");
        }

        var reminderDate = DateTime.UtcNow.AddDays(campaign.ReminderDaysBeforeDeadline);

        var pendingReviews = await _context.AccessCertificationReviews
            .Where(r => r.CampaignId == campaignId &&
                       r.Status == "Pending" &&
                       r.DueDate <= reminderDate)
            .GroupBy(r => r.ReviewerId)
            .Select(g => new { ReviewerId = g.Key, Count = g.Count() })
            .ToListAsync();

        foreach (var reviewerGroup in pendingReviews)
        {
            _logger.LogInformation("Reminder: Reviewer {ReviewerId} has {Count} pending reviews",
                reviewerGroup.ReviewerId, reviewerGroup.Count);
        }

        _logger.LogInformation("Sent reminders to {Count} reviewers", pendingReviews.Count);
    }

    // ============================================
    // Private Helper Methods
    // ============================================

    private async Task GenerateReviewsForCampaignAsync(
        AccessCertificationCampaign campaign,
        List<string>? targetRoles,
        List<Guid>? targetUserIds)
    {
        var userRoles = await _context.UserRoles
            .Include(ur => ur.User)
            .Include(ur => ur.Role)
            .ToListAsync();

        if (targetRoles != null && targetRoles.Any())
        {
            userRoles = userRoles.Where(ur => targetRoles.Contains(ur.Role.Name)).ToList();
        }

        if (targetUserIds != null && targetUserIds.Any())
        {
            userRoles = userRoles.Where(ur => targetUserIds.Contains(ur.UserId)).ToList();
        }

        foreach (var userRole in userRoles)
        {
            var reviewerId = userRole.UserId;

            var review = new AccessCertificationReview
            {
                Id = Guid.NewGuid(),
                CampaignId = campaign.Id,
                UserId = userRole.UserId,
                AccessType = "Role",
                AccessValue = userRole.Role.Name,
                ReviewerId = reviewerId,
                Status = "Pending",
                DueDate = campaign.EndDate,
                CreatedAt = DateTime.UtcNow
            };

            _context.AccessCertificationReviews.Add(review);
        }

        await _context.SaveChangesAsync();
    }

    private async Task<CertificationCampaignDto> MapCampaignToDtoAsync(AccessCertificationCampaign campaign)
    {
        var stats = await GetCampaignStatisticsAsync(campaign.Id);

        return new CertificationCampaignDto
        {
            Id = campaign.Id,
            Name = campaign.Name,
            Description = campaign.Description,
            CampaignType = campaign.CampaignType,
            Status = campaign.Status,
            StartDate = campaign.StartDate,
            EndDate = campaign.EndDate,
            AutoRevokeOnExpiry = campaign.AutoRevokeOnExpiry,
            TotalReviews = stats.TotalReviews,
            CompletedReviews = stats.CompletedReviews,
            ApprovedCount = stats.ApprovedCount,
            RevokedCount = stats.RevokedCount,
            InitiatedBy = campaign.InitiatedBy,
            CreatedAt = campaign.CreatedAt
        };
    }

    private async Task RevokeUserAccessAsync(Guid userId, string accessType, string accessValue)
    {
        if (accessType == "Role")
        {
            var userRole = await _context.UserRoles
                .Include(ur => ur.Role)
                .FirstOrDefaultAsync(ur => ur.UserId == userId && ur.Role.Name == accessValue);

            if (userRole != null)
            {
                _context.UserRoles.Remove(userRole);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Revoked role {RoleName} from user {UserId}", accessValue, userId);
            }
        }
    }
}
