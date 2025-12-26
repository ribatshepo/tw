using Microsoft.AspNetCore.Identity;

namespace USP.Core.Models.Entities;

/// <summary>
/// Application user entity with authentication and security features
/// </summary>
public class ApplicationUser : IdentityUser<Guid>
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string Status { get; set; } = "active";
    public bool MfaEnabled { get; set; }
    public string? MfaSecret { get; set; }

    // SMS/Phone MFA
    public string? VerifiedPhoneNumber { get; set; }
    public bool PhoneNumberVerified { get; set; } = false;

    public int FailedLoginAttempts { get; set; }
    public DateTime? LastFailedLogin { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DateTime? LockedUntil { get; set; }
    public DateTime? PasswordChangedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
    public string? Metadata { get; set; } // JSON: additional user metadata
    public string? RiskProfile { get; set; } // JSON: user risk assessment data
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public virtual ICollection<Session> Sessions { get; set; } = new List<Session>();
    public virtual ICollection<ApiKey> ApiKeys { get; set; } = new List<ApiKey>();
    public virtual ICollection<Secret> CreatedSecrets { get; set; } = new List<Secret>();
    public virtual ICollection<SecretAccessLog> SecretAccessLogs { get; set; } = new List<SecretAccessLog>();
    public virtual ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
    public virtual ICollection<MfaDevice> MfaDevices { get; set; } = new List<MfaDevice>();
    public virtual ICollection<MfaBackupCode> MfaBackupCodes { get; set; } = new List<MfaBackupCode>();
    public virtual ICollection<TrustedDevice> TrustedDevices { get; set; } = new List<TrustedDevice>();
}
