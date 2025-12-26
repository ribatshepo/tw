namespace USP.Core.Domain.Enums;

/// <summary>
/// Represents the status of a user account
/// </summary>
public enum UserStatus
{
    /// <summary>
    /// User account is active and can authenticate
    /// </summary>
    Active = 0,

    /// <summary>
    /// User account is temporarily inactive (can be reactivated)
    /// </summary>
    Inactive = 1,

    /// <summary>
    /// User account is locked due to failed login attempts
    /// </summary>
    Locked = 2,

    /// <summary>
    /// User account is suspended by administrator
    /// </summary>
    Suspended = 3,

    /// <summary>
    /// User account is pending email verification
    /// </summary>
    PendingVerification = 4,

    /// <summary>
    /// User account is soft-deleted (can be restored)
    /// </summary>
    Deleted = 5
}
