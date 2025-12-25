namespace USP.Core.Models.Entities;

/// <summary>
/// MFA backup recovery codes
/// </summary>
public class MfaBackupCode
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string CodeHash { get; set; } = string.Empty;
    public bool IsUsed { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UsedAt { get; set; }

    // Navigation properties
    public virtual ApplicationUser User { get; set; } = null!;
}
