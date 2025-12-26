using System.ComponentModel.DataAnnotations;

namespace USP.Core.Domain.Entities.PAM;

/// <summary>
/// Represents a container for privileged accounts with RBAC
/// </summary>
public class Safe
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public string? Metadata { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string? CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? DeletedAt { get; set; }

    public virtual ICollection<PrivilegedAccount> Accounts { get; set; } = new List<PrivilegedAccount>();
}
