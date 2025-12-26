using System.ComponentModel.DataAnnotations;

namespace USP.Core.Domain.Entities.PAM;

/// <summary>
/// Represents a privileged account stored in a safe
/// </summary>
public class PrivilegedAccount
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    public string SafeId { get; set; } = null!;

    [Required]
    [MaxLength(255)]
    public string AccountName { get; set; } = null!;

    [Required]
    [MaxLength(255)]
    public string Platform { get; set; } = null!; // e.g., "Windows", "Linux", "Database"

    [MaxLength(500)]
    public string? Address { get; set; } // Hostname or IP

    public string? EncryptedPassword { get; set; }

    public string? EncryptedSSHKey { get; set; }

    public bool AutoRotationEnabled { get; set; }

    public int? RotationIntervalDays { get; set; }

    public DateTime? LastRotatedAt { get; set; }

    public DateTime? NextRotationAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? DeletedAt { get; set; }

    public virtual Safe Safe { get; set; } = null!;

    public virtual ICollection<Checkout> Checkouts { get; set; } = new List<Checkout>();
}
