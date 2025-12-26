namespace USP.Core.Models.DTOs.UserLifecycle;

/// <summary>
/// DTO for access certification campaign
/// </summary>
public class AccessCertificationCampaignDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string CampaignType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Frequency { get; set; } = string.Empty;
    public int TotalReviewsRequired { get; set; }
    public int ReviewsCompleted { get; set; }
    public int ReviewsApproved { get; set; }
    public int ReviewsRejected { get; set; }
    public int ReviewsPending { get; set; }
    public int AccessRevoked { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

/// <summary>
/// Request to create an access certification campaign
/// </summary>
public class CreateAccessCertificationCampaignRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string CampaignType { get; set; } = "user_access";
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Frequency { get; set; } = "quarterly";
    public string Scope { get; set; } = "all_users";
    public string? ScopeFilter { get; set; }
    public int ReviewerResponseDeadlineDays { get; set; } = 14;
    public bool AllowSelfReview { get; set; } = false;
    public bool RequireJustification { get; set; } = true;
    public bool AutoRevokeOnNonResponse { get; set; } = false;
    public bool AutoRevokeOnRejection { get; set; } = true;
}

/// <summary>
/// DTO for individual access certification review
/// </summary>
public class AccessCertificationReviewDto
{
    public Guid Id { get; set; }
    public Guid CampaignId { get; set; }
    public string CampaignName { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public Guid ReviewerId { get; set; }
    public string ReviewerName { get; set; } = string.Empty;
    public string SubjectType { get; set; } = string.Empty;
    public string SubjectName { get; set; } = string.Empty;
    public string SubjectDescription { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? ReviewedAt { get; set; }
    public DateTime DueDate { get; set; }
    public string? ReviewerDecision { get; set; }
    public string? ReviewerJustification { get; set; }
    public string? RiskLevel { get; set; }
    public bool AccessRevoked { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Request to submit an access certification review decision
/// </summary>
public class SubmitAccessCertificationReviewRequest
{
    public string Decision { get; set; } = string.Empty; // "approve", "reject"
    public string Justification { get; set; } = string.Empty;
    public string? Comments { get; set; }
}
