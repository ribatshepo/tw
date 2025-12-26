namespace USP.Core.Models.Entities.Rotation;

/// <summary>
/// Audit history of certificate rotation operations
/// </summary>
public class CertificateRotationHistory
{
    public Guid Id { get; set; }
    public Guid CertificateRotationId { get; set; }
    public string Action { get; set; } = string.Empty; // rotated, renewed, deployed, revoked, failed
    public string? OldCertificateThumbprint { get; set; }
    public string? NewCertificateThumbprint { get; set; }
    public DateTime? OldExpirationDate { get; set; }
    public DateTime? NewExpirationDate { get; set; }
    public string Status { get; set; } = "success"; // success, failed, partial
    public string? ErrorMessage { get; set; }
    public string? DeploymentResults { get; set; } // JSON of deployment results per target
    public TimeSpan? RotationDuration { get; set; }
    public bool ChainValid { get; set; } = true;
    public string? ChainValidationDetails { get; set; }
    public Guid? InitiatedByUserId { get; set; }
    public string InitiationType { get; set; } = "automatic"; // automatic, manual, event-triggered
    public string? CorrelationId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
