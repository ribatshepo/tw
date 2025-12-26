using System.ComponentModel.DataAnnotations;

namespace USP.Core.Domain.Entities.Integration;

/// <summary>
/// Represents a multi-tenancy workspace
/// </summary>
public class Workspace
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = null!;

    [MaxLength(100)]
    public string? Slug { get; set; }  // URL-friendly identifier

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public string? Settings { get; set; }  // JSON

    public string? Metadata { get; set; }  // JSON

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string? CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? DeletedAt { get; set; }
}
