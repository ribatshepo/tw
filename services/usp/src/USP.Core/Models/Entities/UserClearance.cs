namespace USP.Core.Models.Entities;

/// <summary>
/// Tracks workforce security clearances for HIPAA compliance §164.308(a)(3)
/// Ensures proper background checks and authorization procedures
/// </summary>
public class UserClearance
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string ClearanceType { get; set; } = null!;
    public string Status { get; set; } = null!;
    public DateTime GrantedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public Guid GrantedBy { get; set; }
    public string? ClearanceLevel { get; set; }
    public string? BackgroundCheckDetails { get; set; }
    public string? BackgroundCheckProvider { get; set; }
    public DateTime? BackgroundCheckCompletedAt { get; set; }
    public string? TrainingCompleted { get; set; }
    public DateTime? LastReviewedAt { get; set; }
    public Guid? LastReviewedBy { get; set; }
    public string? ReviewNotes { get; set; }
    public string? DocumentationPath { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual ApplicationUser User { get; set; } = null!;
    public virtual ApplicationUser GrantedByUser { get; set; } = null!;
    public virtual ApplicationUser? LastReviewedByUser { get; set; }
}
