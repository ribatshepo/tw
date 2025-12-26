using System.ComponentModel.DataAnnotations;
using USP.Core.Domain.Enums;

namespace USP.Core.Domain.Entities.Audit;

/// <summary>
/// Represents a credential rotation policy with scheduling
/// </summary>
public class RotationPolicy
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    [Required]
    public RotationType Type { get; set; }

    [Required]
    public int IntervalDays { get; set; }

    public bool Enabled { get; set; } = true;

    [MaxLength(100)]
    public string? CronSchedule { get; set; }

    public string? Configuration { get; set; } // JSON

    public DateTime? LastExecutedAt { get; set; }

    public DateTime? NextExecutionAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? DeletedAt { get; set; }
}
