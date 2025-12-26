namespace USP.Core.Domain.Enums;

/// <summary>
/// Represents the type of audit event
/// </summary>
public enum AuditEventType
{
    // Authentication Events (0-99)
    /// <summary>
    /// User login succeeded
    /// </summary>
    AuthenticationSuccess = 0,

    /// <summary>
    /// User login failed
    /// </summary>
    AuthenticationFailure = 1,

    /// <summary>
    /// User logged out
    /// </summary>
    Logout = 2,

    /// <summary>
    /// MFA verification succeeded
    /// </summary>
    MFASuccess = 3,

    /// <summary>
    /// MFA verification failed
    /// </summary>
    MFAFailure = 4,

    /// <summary>
    /// Password changed
    /// </summary>
    PasswordChanged = 5,

    /// <summary>
    /// Password reset requested
    /// </summary>
    PasswordResetRequested = 6,

    // Authorization Events (100-199)
    /// <summary>
    /// Authorization check succeeded
    /// </summary>
    AuthorizationGranted = 100,

    /// <summary>
    /// Authorization check failed (access denied)
    /// </summary>
    AuthorizationDenied = 101,

    /// <summary>
    /// Role assigned to user
    /// </summary>
    RoleAssigned = 102,

    /// <summary>
    /// Permission granted
    /// </summary>
    PermissionGranted = 103,

    // Secret Management Events (200-299)
    /// <summary>
    /// Secret created or updated
    /// </summary>
    SecretWritten = 200,

    /// <summary>
    /// Secret read/accessed
    /// </summary>
    SecretRead = 201,

    /// <summary>
    /// Secret deleted
    /// </summary>
    SecretDeleted = 202,

    /// <summary>
    /// Secret restored from soft delete
    /// </summary>
    SecretRestored = 203,

    /// <summary>
    /// Encryption key rotated
    /// </summary>
    KeyRotated = 204,

    /// <summary>
    /// Certificate issued
    /// </summary>
    CertificateIssued = 205,

    /// <summary>
    /// Certificate revoked
    /// </summary>
    CertificateRevoked = 206,

    // PAM Events (300-399)
    /// <summary>
    /// Privileged account checked out
    /// </summary>
    AccountCheckedOut = 300,

    /// <summary>
    /// Privileged account checked in
    /// </summary>
    AccountCheckedIn = 301,

    /// <summary>
    /// Session recording started
    /// </summary>
    SessionRecordingStarted = 302,

    /// <summary>
    /// Session terminated
    /// </summary>
    SessionTerminated = 303,

    /// <summary>
    /// JIT access granted
    /// </summary>
    JITAccessGranted = 304,

    /// <summary>
    /// Break-glass emergency access activated
    /// </summary>
    BreakGlassActivated = 305,

    // Rotation Events (400-499)
    /// <summary>
    /// Credential rotation started
    /// </summary>
    RotationStarted = 400,

    /// <summary>
    /// Credential rotation completed successfully
    /// </summary>
    RotationCompleted = 401,

    /// <summary>
    /// Credential rotation failed
    /// </summary>
    RotationFailed = 402,

    /// <summary>
    /// Credential rotation rolled back
    /// </summary>
    RotationRolledBack = 403,

    // Security Events (500-599)
    /// <summary>
    /// Anomaly detected by threat analytics
    /// </summary>
    AnomalyDetected = 500,

    /// <summary>
    /// Account locked due to suspicious activity
    /// </summary>
    AccountLocked = 501,

    /// <summary>
    /// Impossible travel detected
    /// </summary>
    ImpossibleTravel = 502,

    /// <summary>
    /// Brute force attack detected
    /// </summary>
    BruteForceDetected = 503,

    // System Events (600-699)
    /// <summary>
    /// Vault sealed
    /// </summary>
    VaultSealed = 600,

    /// <summary>
    /// Vault unsealed
    /// </summary>
    VaultUnsealed = 601,

    /// <summary>
    /// Vault initialized
    /// </summary>
    VaultInitialized = 602,

    /// <summary>
    /// Configuration changed
    /// </summary>
    ConfigurationChanged = 603,

    // Compliance Events (700-799)
    /// <summary>
    /// Compliance report generated
    /// </summary>
    ComplianceReportGenerated = 700,

    /// <summary>
    /// Audit export requested
    /// </summary>
    AuditExported = 701
}
