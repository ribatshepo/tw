namespace USP.Core.Models.DTOs.Rotation;

/// <summary>
/// Data transfer object for certificate rotation
/// </summary>
public class CertificateRotationDto
{
    public Guid Id { get; set; }
    public string CertificateName { get; set; } = string.Empty;
    public string CertificateType { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string IssuerType { get; set; } = string.Empty;
    public DateTime ExpirationDate { get; set; }
    public DateTime? LastRotationDate { get; set; }
    public DateTime? NextRotationDate { get; set; }
    public int RotationIntervalDays { get; set; }
    public string RotationPolicy { get; set; } = string.Empty;
    public string? CronExpression { get; set; }
    public bool AutoDeploy { get; set; }
    public int DaysUntilExpiration { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? LastRotationStatus { get; set; }
    public int AlertThresholdDays { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Request to create certificate rotation configuration
/// </summary>
public class CreateCertificateRotationRequest
{
    public string CertificateName { get; set; } = string.Empty;
    public string CertificateType { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string IssuerType { get; set; } = string.Empty;
    public string? AcmeAccountUrl { get; set; }
    public string? DomainValidationType { get; set; }
    public DateTime ExpirationDate { get; set; }
    public int RotationIntervalDays { get; set; } = 30;
    public string RotationPolicy { get; set; } = "automatic";
    public string? CronExpression { get; set; }
    public bool AutoDeploy { get; set; }
    public List<string>? DeploymentTargets { get; set; }
    public int AlertThresholdDays { get; set; } = 30;
    public string? NotificationEmail { get; set; }
    public string? NotificationWebhook { get; set; }
    public Dictionary<string, string>? Tags { get; set; }
}

/// <summary>
/// Request to update certificate rotation configuration
/// </summary>
public class UpdateCertificateRotationRequest
{
    public int? RotationIntervalDays { get; set; }
    public string? RotationPolicy { get; set; }
    public string? CronExpression { get; set; }
    public bool? AutoDeploy { get; set; }
    public List<string>? DeploymentTargets { get; set; }
    public int? AlertThresholdDays { get; set; }
    public string? NotificationEmail { get; set; }
    public string? NotificationWebhook { get; set; }
    public Dictionary<string, string>? Tags { get; set; }
}

/// <summary>
/// Result of certificate rotation operation
/// </summary>
public class CertificateRotationResultDto
{
    public Guid RotationHistoryId { get; set; }
    public bool Success { get; set; }
    public string? NewCertificateThumbprint { get; set; }
    public DateTime? NewExpirationDate { get; set; }
    public bool ChainValid { get; set; }
    public Dictionary<string, bool>? DeploymentResults { get; set; }
    public string? ErrorMessage { get; set; }
    public TimeSpan RotationDuration { get; set; }
    public DateTime CompletedAt { get; set; }
}

/// <summary>
/// Certificate rotation history entry
/// </summary>
public class CertificateRotationHistoryDto
{
    public Guid Id { get; set; }
    public Guid CertificateRotationId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? OldCertificateThumbprint { get; set; }
    public string? NewCertificateThumbprint { get; set; }
    public DateTime? OldExpirationDate { get; set; }
    public DateTime? NewExpirationDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public bool ChainValid { get; set; }
    public TimeSpan? RotationDuration { get; set; }
    public string InitiationType { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Certificate expiration alert
/// </summary>
public class CertificateExpirationAlertDto
{
    public Guid CertificateRotationId { get; set; }
    public string CertificateName { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public DateTime ExpirationDate { get; set; }
    public int DaysUntilExpiration { get; set; }
    public string Severity { get; set; } = string.Empty; // critical, warning, info
    public bool AutoRotationEnabled { get; set; }
}

/// <summary>
/// Certificate rotation statistics
/// </summary>
public class CertificateRotationStatisticsDto
{
    public int TotalCertificates { get; set; }
    public int ActiveCertificates { get; set; }
    public int ExpiringWithin30Days { get; set; }
    public int ExpiringWithin14Days { get; set; }
    public int ExpiringWithin7Days { get; set; }
    public int ExpiringWithin1Day { get; set; }
    public int RotationsLast30Days { get; set; }
    public int FailedRotationsLast30Days { get; set; }
    public double SuccessRate { get; set; }
}
