namespace USP.Core.Models.Entities;

/// <summary>
/// Stores seal configuration for the security platform
/// Contains encrypted master key shares and seal parameters
/// </summary>
public class SealConfiguration
{
    public Guid Id { get; set; }
    public int Version { get; set; } = 1;
    public int SecretThreshold { get; set; } // Number of shares needed to unseal
    public int SecretShares { get; set; } // Total number of shares
    public string EncryptedMasterKey { get; set; } = string.Empty; // Encrypted with reconstructed key from shares
    public bool Initialized { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastUnsealedAt { get; set; }
}
