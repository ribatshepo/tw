namespace USP.Core.Models.Entities;

/// <summary>
/// Represents a time-bound lease for secret access
/// Leases provide temporary access to secrets with automatic expiration and renewal capabilities
/// </summary>
public class Lease
{
    public Guid Id { get; set; }

    /// <summary>
    /// The secret this lease grants access to
    /// </summary>
    public Guid SecretId { get; set; }

    /// <summary>
    /// User who owns this lease
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// When the lease was initially issued
    /// </summary>
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the lease will expire
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Number of times this lease has been renewed
    /// </summary>
    public int RenewalCount { get; set; } = 0;

    /// <summary>
    /// Whether this lease should be automatically renewed before expiration
    /// </summary>
    public bool AutoRenewalEnabled { get; set; } = false;

    /// <summary>
    /// Current status: active, expired, revoked
    /// </summary>
    public string Status { get; set; } = "active";

    /// <summary>
    /// Maximum number of renewals allowed (null = unlimited)
    /// </summary>
    public int? MaxRenewals { get; set; }

    /// <summary>
    /// When the lease was last renewed
    /// </summary>
    public DateTime? LastRenewedAt { get; set; }

    /// <summary>
    /// When the lease was revoked (if applicable)
    /// </summary>
    public DateTime? RevokedAt { get; set; }

    /// <summary>
    /// User who revoked the lease
    /// </summary>
    public Guid? RevokedBy { get; set; }

    /// <summary>
    /// Reason for revocation
    /// </summary>
    public string? RevocationReason { get; set; }

    /// <summary>
    /// Duration of each lease/renewal in seconds
    /// </summary>
    public int LeaseDurationSeconds { get; set; } = 3600; // 1 hour default

    /// <summary>
    /// Additional metadata as JSON
    /// </summary>
    public string? Metadata { get; set; }

    // Navigation properties
    public virtual Secret Secret { get; set; } = null!;
    public virtual ApplicationUser User { get; set; } = null!;
    public virtual ICollection<LeaseRenewalHistory> RenewalHistory { get; set; } = new List<LeaseRenewalHistory>();
}

/// <summary>
/// Tracks the history of lease renewals
/// </summary>
public class LeaseRenewalHistory
{
    public Guid Id { get; set; }

    /// <summary>
    /// The lease that was renewed
    /// </summary>
    public Guid LeaseId { get; set; }

    /// <summary>
    /// When the renewal occurred
    /// </summary>
    public DateTime RenewedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Previous expiration time before renewal
    /// </summary>
    public DateTime PreviousExpiresAt { get; set; }

    /// <summary>
    /// New expiration time after renewal
    /// </summary>
    public DateTime NewExpiresAt { get; set; }

    /// <summary>
    /// Renewal count at the time of this renewal
    /// </summary>
    public int RenewalCount { get; set; }

    /// <summary>
    /// Whether the renewal succeeded
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if renewal failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// User who triggered the renewal (null for auto-renewals)
    /// </summary>
    public Guid? RenewedBy { get; set; }

    /// <summary>
    /// Whether this was an automatic renewal
    /// </summary>
    public bool IsAutoRenewal { get; set; }

    // Navigation property
    public virtual Lease Lease { get; set; } = null!;
}
