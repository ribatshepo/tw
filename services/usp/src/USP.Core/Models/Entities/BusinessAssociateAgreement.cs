namespace USP.Core.Models.Entities;

/// <summary>
/// Tracks Business Associate Agreements (BAA) for HIPAA compliance
/// Required for third-party service providers who handle PHI
/// </summary>
public class BusinessAssociateAgreement
{
    public Guid Id { get; set; }
    public string PartnerId { get; set; } = null!;
    public string PartnerName { get; set; } = null!;
    public string? PartnerContactEmail { get; set; }
    public string? PartnerContactPhone { get; set; }
    public string Status { get; set; } = null!;
    public DateTime EffectiveDate { get; set; }
    public DateTime ExpirationDate { get; set; }
    public string? DocumentUrl { get; set; }
    public string? DocumentPath { get; set; }
    public string? DocumentHash { get; set; }
    public DateTime? LastReviewedAt { get; set; }
    public Guid? LastReviewedBy { get; set; }
    public string? ReviewNotes { get; set; }
    public string? ServicesProvided { get; set; }
    public string? PhiCategories { get; set; }
    public bool RequiresAnnualReview { get; set; } = true;
    public int NotifyDaysBeforeExpiration { get; set; } = 30;
    public DateTime? RenewalRequestedAt { get; set; }
    public DateTime? RenewalCompletedAt { get; set; }
    public string? ComplianceNotes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual ApplicationUser? LastReviewedByUser { get; set; }
}
