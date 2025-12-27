namespace USP.Core.Domain.Entities.Vault;

/// <summary>
/// Stores the vault seal configuration.
/// This entity should only have one record in the database.
/// </summary>
public class SealConfiguration
{
    /// <summary>
    /// Primary key (should always be "default").
    /// </summary>
    public string Id { get; set; } = "default";

    /// <summary>
    /// Whether the vault has been initialized.
    /// </summary>
    public bool Initialized { get; set; }

    /// <summary>
    /// Number of key shares generated during initialization.
    /// </summary>
    public int SecretShares { get; set; }

    /// <summary>
    /// Number of key shares required to unseal.
    /// </summary>
    public int SecretThreshold { get; set; }

    /// <summary>
    /// Encrypted master key (encrypted with the reconstructed key from Shamir shares).
    /// This is stored as a verification mechanism.
    /// </summary>
    public byte[]? EncryptedMasterKey { get; set; }

    /// <summary>
    /// Cluster name.
    /// </summary>
    public string ClusterName { get; set; } = "usp-cluster";

    /// <summary>
    /// Cluster ID (unique identifier).
    /// </summary>
    public string ClusterId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Timestamp when the vault was initialized.
    /// </summary>
    public DateTime? InitializedAt { get; set; }

    /// <summary>
    /// Timestamp when last sealed.
    /// </summary>
    public DateTime? LastSealedAt { get; set; }

    /// <summary>
    /// Timestamp when last unsealed.
    /// </summary>
    public DateTime? LastUnsealedAt { get; set; }

    /// <summary>
    /// Version of the vault.
    /// </summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// Timestamp when created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Timestamp when last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
