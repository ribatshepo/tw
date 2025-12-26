namespace USP.Core.Models.Entities;

/// <summary>
/// Access certification campaign for periodic access reviews and attestation
/// Ensures compliance with SOC 2, HIPAA, and other regulatory requirements
/// </summary>
public class AccessCertificationCampaign
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string CampaignType { get; set; } = "user_access"; // "user_access", "privileged_access", "role_membership"
    public string Status { get; set; } = "draft"; // "draft", "active", "completed", "cancelled"

    // Schedule
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Frequency { get; set; } = "quarterly"; // "quarterly", "semi_annual", "annual", "one_time"

    // Scope
    public string Scope { get; set; } = "all_users"; // "all_users", "privileged_users", "specific_roles", "specific_users"
    public string? ScopeFilter { get; set; } // JSON filter criteria

    // Review settings
    public int ReviewerResponseDeadlineDays { get; set; } = 14;
    public bool AllowSelfReview { get; set; } = false;
    public bool RequireJustification { get; set; } = true;
    public bool AutoRevokeOnNonResponse { get; set; } = false;
    public bool AutoRevokeOnRejection { get; set; } = true;

    // Escalation
    public int EscalationDays { get; set; } = 7;
    public Guid? EscalationManagerId { get; set; }

    // Statistics
    public int TotalReviewsRequired { get; set; }
    public int ReviewsCompleted { get; set; }
    public int ReviewsApproved { get; set; }
    public int ReviewsRejected { get; set; }
    public int ReviewsPending { get; set; }
    public int AccessRevoked { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public Guid CreatedBy { get; set; }

    // Navigation properties
    public virtual ICollection<AccessCertificationReview> Reviews { get; set; } = new List<AccessCertificationReview>();
}
