using System.ComponentModel.DataAnnotations;
using USP.Core.Domain.Enums;

namespace USP.Core.Domain.Entities.PAM;

/// <summary>
/// Represents a checkout/checkin workflow for a privileged account
/// </summary>
public class Checkout
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    public string AccountId { get; set; } = null!;

    [Required]
    public string UserId { get; set; } = null!;

    [Required]
    public CheckoutStatus Status { get; set; }

    public string? Reason { get; set; }

    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ApprovedAt { get; set; }

    public string? ApprovedBy { get; set; }

    public DateTime? CheckedOutAt { get; set; }

    public DateTime? CheckedInAt { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public bool PasswordRotatedOnCheckin { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public virtual PrivilegedAccount Account { get; set; } = null!;

    public bool IsActive() => Status == CheckoutStatus.Active && (!ExpiresAt.HasValue || ExpiresAt.Value > DateTime.UtcNow);
}
