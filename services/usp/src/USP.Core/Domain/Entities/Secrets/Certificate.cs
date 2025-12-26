using System.ComponentModel.DataAnnotations;

namespace USP.Core.Domain.Entities.Secrets;

/// <summary>
/// Represents a PKI certificate issued by the CA
/// </summary>
public class Certificate
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    [MaxLength(255)]
    public string SerialNumber { get; set; } = null!;

    [Required]
    [MaxLength(500)]
    public string Subject { get; set; } = null!;

    [Required]
    [MaxLength(500)]
    public string Issuer { get; set; } = null!;

    [Required]
    public string CertificateData { get; set; } = null!;

    public string? PrivateKeyData { get; set; }

    public DateTime NotBefore { get; set; }

    public DateTime NotAfter { get; set; }

    public bool IsRevoked { get; set; }

    public DateTime? RevokedAt { get; set; }

    [MaxLength(255)]
    public string? RevocationReason { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? DeletedAt { get; set; }

    public bool IsExpired() => DateTime.UtcNow > NotAfter;

    public bool IsValid() => !IsRevoked && !IsExpired() && DeletedAt == null;
}
