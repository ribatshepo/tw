namespace USP.Core.Models.DTOs.PAM;

/// <summary>
/// Request to create an approval
/// </summary>
public class CreateApprovalRequest
{
    public string ResourceType { get; set; } = string.Empty; // PrivilegedAccount, Safe, etc.
    public Guid ResourceId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string ApprovalPolicy { get; set; } = "single_approver"; // single_approver, dual_control, all_approvers, majority
    public List<Guid> Approvers { get; set; } = new(); // List of approver user IDs
    public int ExpirationHours { get; set; } = 24;
}

/// <summary>
/// Access approval information
/// </summary>
public class AccessApprovalDto
{
    public Guid Id { get; set; }
    public Guid RequesterId { get; set; }
    public string RequesterEmail { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public Guid ResourceId { get; set; }
    public string ResourceName { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty; // pending, approved, denied, expired, cancelled
    public string ApprovalPolicy { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public List<ApproverActionDto> ApproverActions { get; set; } = new();
    public int RequiredApprovals { get; set; }
    public int CurrentApprovals { get; set; }
    public bool IsExpired => DateTime.UtcNow > ExpiresAt && Status == "pending";
    public int RemainingHours => Status == "pending" ? Math.Max(0, (int)(ExpiresAt - DateTime.UtcNow).TotalHours) : 0;
}

/// <summary>
/// Approver action details
/// </summary>
public class ApproverActionDto
{
    public Guid ApproverId { get; set; }
    public string ApproverEmail { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty; // approved, denied, pending
    public DateTime? ActionAt { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// Approval statistics
/// </summary>
public class ApprovalStatisticsDto
{
    public int PendingApprovals { get; set; }
    public int TotalApprovals { get; set; }
    public int ApprovalsLast24Hours { get; set; }
    public int ApprovalsLast7Days { get; set; }
    public int ApprovalsLast30Days { get; set; }
    public int ApprovedCount { get; set; }
    public int DeniedCount { get; set; }
    public int ExpiredCount { get; set; }
    public double AverageApprovalTimeMinutes { get; set; }
    public List<ApprovalSummaryDto> RecentApprovals { get; set; } = new();
}

/// <summary>
/// Approval summary
/// </summary>
public class ApprovalSummaryDto
{
    public Guid ApprovalId { get; set; }
    public string ResourceType { get; set; } = string.Empty;
    public string ResourceName { get; set; } = string.Empty;
    public string RequesterEmail { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public string ApprovalPolicy { get; set; } = string.Empty;
}
