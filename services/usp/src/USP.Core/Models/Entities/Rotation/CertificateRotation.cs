namespace USP.Core.Models.Entities.Rotation;

/// <summary>
/// Represents a certificate rotation operation
/// </summary>
public class CertificateRotation
{
    public Guid Id { get; set; }
    public string CertificateName { get; set; } = string.Empty;
    public string CertificateType { get; set; } = string.Empty; // TLS, ClientAuth, CodeSigning, etc.
    public string Subject { get; set; } = string.Empty;
    public string IssuerType { get; set; } = string.Empty; // LetsEncrypt, PrivateCA, PublicCA
    public string? AcmeAccountUrl { get; set; }
    public string? DomainValidationType { get; set; } // HTTP-01, DNS-01, TLS-ALPN-01
    public DateTime ExpirationDate { get; set; }
    public DateTime? LastRotationDate { get; set; }
    public DateTime? NextRotationDate { get; set; }
    public int RotationIntervalDays { get; set; } = 30;
    public string RotationPolicy { get; set; } = "automatic"; // automatic, manual, scheduled
    public string? CronExpression { get; set; }
    public bool AutoDeploy { get; set; } = false;
    public string? DeploymentTargets { get; set; } // JSON array of target systems
    public string Status { get; set; } = "active"; // active, rotating, failed, disabled
    public string? LastRotationStatus { get; set; }
    public string? LastRotationError { get; set; }
    public int AlertThresholdDays { get; set; } = 30; // Alert when expiration is within this many days
    public bool AlertSent30Days { get; set; }
    public bool AlertSent14Days { get; set; }
    public bool AlertSent7Days { get; set; }
    public bool AlertSent1Day { get; set; }
    public string? NotificationEmail { get; set; }
    public string? NotificationWebhook { get; set; }
    public Guid? OwnerId { get; set; }
    public string? Tags { get; set; } // JSON for metadata
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
