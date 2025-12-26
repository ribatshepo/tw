using System.ComponentModel.DataAnnotations;
using USP.Core.Domain.Enums;

namespace USP.Core.Domain.Entities.Secrets;

/// <summary>
/// Represents an encryption key in the Transit engine
/// </summary>
public class EncryptionKey
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = null!;

    [Required]
    public EncryptionAlgorithm Algorithm { get; set; }

    public int CurrentVersion { get; set; } = 1;

    public int MinDecryptionVersion { get; set; } = 1;

    public bool AllowPlaintextBackup { get; set; }

    public bool Exportable { get; set; }

    public bool DeletionAllowed { get; set; } = true;

    public bool ConvergentEncryption { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? DeletedAt { get; set; }
}
