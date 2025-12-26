using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using USP.Core.Domain.Enums;

namespace USP.Core.Domain.Entities.Identity;

/// <summary>
/// Represents a user in the USP system with security extensions beyond ASP.NET Core Identity
/// </summary>
public class ApplicationUser : IdentityUser
{
    /// <summary>
    /// First name of the user
    /// </summary>
    [MaxLength(255)]
    public string? FirstName { get; set; }

    /// <summary>
    /// Last name of the user
    /// </summary>
    [MaxLength(255)]
    public string? LastName { get; set; }

    /// <summary>
    /// Full name (computed from FirstName and LastName)
    /// </summary>
    public string FullName => $"{FirstName} {LastName}".Trim();

    /// <summary>
    /// User account status
    /// </summary>
    [Required]
    public UserStatus Status { get; set; } = UserStatus.Active;

    /// <summary>
    /// Indicates whether MFA is enabled for this user
    /// </summary>
    public bool MfaEnabled { get; set; }

    /// <summary>
    /// TOTP secret for MFA (base32 encoded, encrypted)
    /// </summary>
    [MaxLength(255)]
    public string? MfaSecret { get; set; }

    /// <summary>
    /// Number of consecutive failed login attempts
    /// </summary>
    public int FailedLoginAttempts { get; set; }

    /// <summary>
    /// Timestamp of the last failed login attempt
    /// </summary>
    public DateTime? LastFailedLogin { get; set; }

    /// <summary>
    /// Timestamp until which the account is locked (null if not locked)
    /// </summary>
    public DateTime? LockedUntil { get; set; }

    /// <summary>
    /// Timestamp when the password was last changed
    /// </summary>
    public DateTime PasswordChangedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// User's current risk score (0-100, higher = more risky)
    /// </summary>
    [Range(0, 100)]
    public decimal RiskScore { get; set; }

    /// <summary>
    /// Timestamp of last risk score calculation
    /// </summary>
    public DateTime? RiskScoreUpdatedAt { get; set; }

    /// <summary>
    /// Indicates whether the user requires re-authentication (e.g., after password reset)
    /// </summary>
    public bool RequireReauthentication { get; set; }

    /// <summary>
    /// Last successful login timestamp
    /// </summary>
    public DateTime? LastLoginAt { get; set; }

    /// <summary>
    /// IP address of last successful login
    /// </summary>
    [MaxLength(45)] // IPv6 max length
    public string? LastLoginIp { get; set; }

    /// <summary>
    /// Geolocation of last successful login (city, country)
    /// </summary>
    [MaxLength(255)]
    public string? LastLoginLocation { get; set; }

    /// <summary>
    /// Maximum concurrent sessions allowed for this user (null = system default)
    /// </summary>
    public int? MaxConcurrentSessions { get; set; }

    /// <summary>
    /// Metadata stored as JSON (custom attributes, preferences, etc.)
    /// </summary>
    public string? Metadata { get; set; }

    /// <summary>
    /// Timestamp when the record was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Timestamp when the record was last updated
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Soft delete timestamp (null if not deleted)
    /// </summary>
    public DateTime? DeletedAt { get; set; }

    // Navigation properties

    /// <summary>
    /// MFA devices registered for this user
    /// </summary>
    public virtual ICollection<MFADevice> MFADevices { get; set; } = new List<MFADevice>();

    /// <summary>
    /// Trusted devices for this user
    /// </summary>
    public virtual ICollection<TrustedDevice> TrustedDevices { get; set; } = new List<TrustedDevice>();

    /// <summary>
    /// Active sessions for this user
    /// </summary>
    public virtual ICollection<Session> Sessions { get; set; } = new List<Session>();

    /// <summary>
    /// Check if the user's account is currently locked
    /// </summary>
    public bool IsLocked()
    {
        return LockedUntil.HasValue && LockedUntil.Value > DateTime.UtcNow;
    }

    /// <summary>
    /// Check if the user account is active and can authenticate
    /// </summary>
    public bool CanAuthenticate()
    {
        return Status == UserStatus.Active && !IsLocked() && DeletedAt == null;
    }

    /// <summary>
    /// Check if the user has high risk score (>= 75)
    /// </summary>
    public bool IsHighRisk()
    {
        return RiskScore >= 75;
    }
}
