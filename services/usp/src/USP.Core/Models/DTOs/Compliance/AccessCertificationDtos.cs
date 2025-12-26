namespace USP.Core.Models.DTOs.Compliance;

// ============================================
// Campaign Management
// ============================================

public class CreateCertificationCampaignRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string CampaignType { get; set; } = "UserAccess"; // UserAccess, RoleRecertification, PrivilegedAccess
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public List<string>? TargetRoles { get; set; }
    public List<Guid>? TargetUserIds { get; set; }
    public bool AutoRevokeOnExpiry { get; set; } = false;
    public int ReminderDaysBeforeDeadline { get; set; } = 7;
}

public class CertificationCampaignDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string CampaignType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty; // Draft, Active, Completed, Cancelled
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool AutoRevokeOnExpiry { get; set; }
    public int TotalReviews { get; set; }
    public int CompletedReviews { get; set; }
    public int ApprovedCount { get; set; }
    public int RevokedCount { get; set; }
    public Guid InitiatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}

// ============================================
// Access Reviews
// ============================================

public class AccessReviewDto
{
    public Guid Id { get; set; }
    public Guid CampaignId { get; set; }
    public string CampaignName { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public string AccessType { get; set; } = string.Empty; // Role, Permission, Resource
    public string AccessValue { get; set; } = string.Empty;
    public Guid ReviewerId { get; set; }
    public string ReviewerName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty; // Pending, Approved, Revoked, Delegated
    public DateTime DueDate { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewComment { get; set; }
}

public class CertifyAccessRequest
{
    public string? Comment { get; set; }
}

public class RevokeAccessRequest
{
    public string Reason { get; set; } = string.Empty;
    public bool RevokeImmediately { get; set; } = true;
}

// ============================================
// Orphaned Accounts
// ============================================

public class OrphanedAccountDto
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty; // NoManager, Inactive, Suspended
    public DateTime? LastLoginAt { get; set; }
    public int DaysSinceLastLogin { get; set; }
    public List<string> AssignedRoles { get; set; } = new();
}

// ============================================
// Campaign Statistics
// ============================================

public class CampaignStatisticsDto
{
    public Guid CampaignId { get; set; }
    public int TotalReviews { get; set; }
    public int PendingReviews { get; set; }
    public int CompletedReviews { get; set; }
    public int ApprovedCount { get; set; }
    public int RevokedCount { get; set; }
    public int DelegatedCount { get; set; }
    public decimal CompletionPercentage { get; set; }
    public Dictionary<string, int> ReviewerStatistics { get; set; } = new();
    public Dictionary<string, int> AccessTypeBreakdown { get; set; } = new();
}
