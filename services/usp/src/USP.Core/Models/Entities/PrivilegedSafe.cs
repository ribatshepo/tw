namespace USP.Core.Models.Entities;

/// <summary>
/// Privileged safe for storing and managing privileged accounts
/// </summary>
public class PrivilegedSafe
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid OwnerId { get; set; }
    public string SafeType { get; set; } = "Generic"; // Database, SSH, Cloud, Windows, Linux, Generic
    public string AccessControl { get; set; } = string.Empty; // JSON: list of user IDs with permissions
    public bool RequireApproval { get; set; } = false;
    public bool RequireDualControl { get; set; } = false;
    public int MaxCheckoutDurationMinutes { get; set; } = 240; // 4 hours default
    public bool RotateOnCheckin { get; set; } = false;
    public bool SessionRecordingEnabled { get; set; } = false;
    public string? Metadata { get; set; } // JSON: additional safe metadata
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public virtual ApplicationUser Owner { get; set; } = null!;
    public virtual ICollection<PrivilegedAccount> Accounts { get; set; } = new List<PrivilegedAccount>();
}

/// <summary>
/// Privileged account stored in a safe
/// </summary>
public class PrivilegedAccount
{
    public Guid Id { get; set; }
    public Guid SafeId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string EncryptedPassword { get; set; } = string.Empty; // AES-256-GCM encrypted
    public string Platform { get; set; } = string.Empty; // PostgreSQL, MySQL, Windows, Linux, AWS, Azure, SSH
    public string? HostAddress { get; set; } // IP or hostname
    public int? Port { get; set; }
    public string? DatabaseName { get; set; } // For database accounts
    public string? ConnectionDetails { get; set; } // JSON: platform-specific connection details
    public string RotationPolicy { get; set; } = "manual"; // manual, on_checkout, scheduled, on_expiration
    public int RotationIntervalDays { get; set; } = 90;
    public DateTime? LastRotated { get; set; }
    public DateTime? NextRotation { get; set; }
    public string Status { get; set; } = "active"; // active, disabled, expired, rotation_pending
    public int? PasswordComplexity { get; set; } = 16; // Password length for rotation
    public bool RequireMfa { get; set; } = false;
    public string? Metadata { get; set; } // JSON: additional account metadata
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public virtual PrivilegedSafe Safe { get; set; } = null!;
    public virtual ICollection<AccountCheckout> Checkouts { get; set; } = new List<AccountCheckout>();
}

/// <summary>
/// Account checkout/checkin tracking
/// </summary>
public class AccountCheckout
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public Guid UserId { get; set; }
    public DateTime CheckedOutAt { get; set; } = DateTime.UtcNow;
    public DateTime? CheckedInAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public string Reason { get; set; } = string.Empty; // Justification for checkout
    public Guid? ApprovalId { get; set; }
    public string Status { get; set; } = "active"; // active, expired, checked_in
    public bool RotateOnCheckin { get; set; } = false;
    public bool WasRotated { get; set; } = false;
    public string? SessionRecordingPath { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? Metadata { get; set; } // JSON: additional checkout metadata

    // Navigation properties
    public virtual PrivilegedAccount Account { get; set; } = null!;
    public virtual ApplicationUser User { get; set; } = null!;
    public virtual AccessApproval? Approval { get; set; }
}

/// <summary>
/// Access approval for dual control
/// </summary>
public class AccessApproval
{
    public Guid Id { get; set; }
    public Guid RequesterId { get; set; }
    public string ResourceType { get; set; } = string.Empty; // safe, account, jit_access, break_glass
    public Guid ResourceId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = "pending"; // pending, approved, denied, expired
    public string ApprovalPolicy { get; set; } = "any"; // any, all, majority
    public int RequiredApprovals { get; set; } = 1;
    public int CurrentApprovals { get; set; } = 0;
    public List<Guid> Approvers { get; set; } = new(); // List of user IDs who can approve
    public List<Guid> ApprovedBy { get; set; } = new(); // List of user IDs who approved
    public string? DeniedBy { get; set; } // User ID who denied
    public string? DenialReason { get; set; }
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? DeniedAt { get; set; }
    public string? Metadata { get; set; } // JSON: additional approval metadata

    // Navigation properties
    public virtual ApplicationUser Requester { get; set; } = null!;
    public virtual ICollection<AccountCheckout> Checkouts { get; set; } = new List<AccountCheckout>();
}
