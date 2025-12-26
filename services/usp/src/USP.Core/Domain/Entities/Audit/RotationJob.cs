using System.ComponentModel.DataAnnotations;
using USP.Core.Domain.Enums;

namespace USP.Core.Domain.Entities.Audit;

/// <summary>
/// Represents a credential rotation job
/// </summary>
public class RotationJob
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    public RotationType Type { get; set; }

    [Required]
    public RotationStatus Status { get; set; }

    [Required]
    [MaxLength(500)]
    public string TargetResource { get; set; } = null!;

    public string? TargetCredentialId { get; set; }

    public string? PolicyId { get; set; }

    public string? ErrorMessage { get; set; }

    public DateTime ScheduledAt { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public TimeSpan? Duration { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
