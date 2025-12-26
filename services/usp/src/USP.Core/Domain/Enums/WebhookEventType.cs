namespace USP.Core.Domain.Enums;

/// <summary>
/// Represents the type of webhook event that can trigger a webhook delivery
/// </summary>
public enum WebhookEventType
{
    /// <summary>
    /// User created
    /// </summary>
    UserCreated = 0,

    /// <summary>
    /// User deleted
    /// </summary>
    UserDeleted = 1,

    /// <summary>
    /// Secret written or updated
    /// </summary>
    SecretWritten = 2,

    /// <summary>
    /// Secret deleted
    /// </summary>
    SecretDeleted = 3,

    /// <summary>
    /// Credential rotation completed
    /// </summary>
    RotationCompleted = 4,

    /// <summary>
    /// Credential rotation failed
    /// </summary>
    RotationFailed = 5,

    /// <summary>
    /// Certificate revoked
    /// </summary>
    CertificateRevoked = 6,

    /// <summary>
    /// Certificate expiring soon
    /// </summary>
    CertificateExpiring = 7,

    /// <summary>
    /// PAM account checked out
    /// </summary>
    PAMCheckout = 8,

    /// <summary>
    /// PAM account checked in
    /// </summary>
    PAMCheckin = 9,

    /// <summary>
    /// Anomaly detected by threat analytics
    /// </summary>
    AnomalyDetected = 10,

    /// <summary>
    /// Account locked
    /// </summary>
    AccountLocked = 11,

    /// <summary>
    /// Vault sealed
    /// </summary>
    VaultSealed = 12,

    /// <summary>
    /// Vault unsealed
    /// </summary>
    VaultUnsealed = 13
}
