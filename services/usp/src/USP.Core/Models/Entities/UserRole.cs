using Microsoft.AspNetCore.Identity;

namespace USP.Core.Models.Entities;

/// <summary>
/// Junction table for User-Role many-to-many relationship with expiration support
/// </summary>
public class UserRole : IdentityUserRole<Guid>
{
    public DateTime GrantedAt { get; set; } = DateTime.UtcNow;
    public Guid? GrantedBy { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public Guid? NamespaceId { get; set; }
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual ApplicationUser User { get; set; } = null!;
    public virtual Role Role { get; set; } = null!;
    public virtual ApplicationUser? GrantedByUser { get; set; }
}
