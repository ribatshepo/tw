namespace USP.Core.Models.Entities;

/// <summary>
/// Individual access certification review for a specific user's access
/// </summary>
public class AccessCertificationReview
{
    public Guid Id { get; set; }
    public Guid CampaignId { get; set; }
    public Guid UserId { get; set; }
    public Guid ReviewerId { get; set; }

    // Review subject
    public string SubjectType { get; set; } = "user"; // "user", "role", "permission", "safe_access"
    public Guid SubjectId { get; set; }
    public string SubjectName { get; set; } = string.Empty;
    public string SubjectDescription { get; set; } = string.Empty;

    // Review details
    public string Status { get; set; } = "pending"; // "pending", "approved", "rejected", "escalated", "auto_revoked"
    public DateTime? ReviewedAt { get; set; }
    public DateTime DueDate { get; set; }
    public string? ReviewerDecision { get; set; } // "approve", "reject", "needs_review"
    public string? ReviewerJustification { get; set; }
    public string? ReviewerComments { get; set; }

    // Risk assessment
    public string? RiskLevel { get; set; } // "low", "medium", "high", "critical"
    public string? RiskReason { get; set; }

    // Escalation
    public bool IsEscalated { get; set; }
    public DateTime? EscalatedAt { get; set; }
    public Guid? EscalatedToUserId { get; set; }

    // Remediation
    public bool AccessRevoked { get; set; }
    public DateTime? AccessRevokedAt { get; set; }
    public Guid? RevokedByUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual AccessCertificationCampaign Campaign { get; set; } = null!;
    public virtual ApplicationUser User { get; set; } = null!;
    public virtual ApplicationUser Reviewer { get; set; } = null!;
}
