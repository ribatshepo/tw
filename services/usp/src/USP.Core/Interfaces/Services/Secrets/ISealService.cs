namespace USP.Core.Interfaces.Services.Secrets;

/// <summary>
/// Provides seal/unseal operations for the vault using Shamir's Secret Sharing.
/// When the vault is sealed, the master encryption key is not available and
/// no cryptographic operations can be performed.
/// </summary>
public interface ISealService
{
    /// <summary>
    /// Initializes the vault for the first time.
    /// Generates a master key, splits it using Shamir's Secret Sharing,
    /// and returns the unseal keys and root token.
    /// </summary>
    /// <param name="secretShares">Number of key shares to split the master key into (default 5)</param>
    /// <param name="secretThreshold">Number of key shares required to unseal (default 3)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Initialization result with unseal keys and root token</returns>
    Task<InitializeResult> InitializeAsync(
        int secretShares = 5,
        int secretThreshold = 3,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Submits an unseal key to attempt to unseal the vault.
    /// </summary>
    /// <param name="unsealKey">Base64-encoded unseal key</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Seal status after processing the key</returns>
    Task<SealStatus> UnsealAsync(
        string unsealKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Seals the vault, removing the master key from memory.
    /// All cryptographic operations will fail until the vault is unsealed.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Seal status after sealing</returns>
    Task<SealStatus> SealAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current seal status of the vault.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Current seal status</returns>
    Task<SealStatus> GetSealStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the vault is currently sealed.
    /// </summary>
    /// <returns>True if sealed, false if unsealed</returns>
    bool IsSealed();

    /// <summary>
    /// Resets the unseal progress (clears all submitted unseal keys).
    /// </summary>
    Task ResetUnsealProgressAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current master key (only available when unsealed).
    /// Used internally by encryption services to encrypt/decrypt key material.
    /// </summary>
    /// <returns>The master key bytes if unsealed, null if sealed</returns>
    byte[]? GetMasterKey();
}

/// <summary>
/// Result of vault initialization containing unseal keys and root token.
/// </summary>
public class InitializeResult
{
    /// <summary>
    /// Base64-encoded unseal keys (Shamir shares).
    /// These keys should be distributed to different administrators.
    /// </summary>
    public required List<string> UnsealKeys { get; set; }

    /// <summary>
    /// Hex-encoded unseal keys (alternative format).
    /// </summary>
    public required List<string> UnsealKeysHex { get; set; }

    /// <summary>
    /// Base64-encoded root token for initial authentication.
    /// This token has full administrative privileges.
    /// </summary>
    public required string RootToken { get; set; }

    /// <summary>
    /// Number of key shares generated.
    /// </summary>
    public required int SecretShares { get; set; }

    /// <summary>
    /// Number of key shares required to unseal.
    /// </summary>
    public required int SecretThreshold { get; set; }
}

/// <summary>
/// Status of the vault seal.
/// </summary>
public class SealStatus
{
    /// <summary>
    /// Whether the vault is sealed.
    /// </summary>
    public required bool Sealed { get; set; }

    /// <summary>
    /// Number of unseal keys required to unseal the vault.
    /// </summary>
    public required int Threshold { get; set; }

    /// <summary>
    /// Total number of unseal key shares.
    /// </summary>
    public required int SecretShares { get; set; }

    /// <summary>
    /// Number of unseal keys submitted so far.
    /// </summary>
    public required int Progress { get; set; }

    /// <summary>
    /// Whether the vault has been initialized.
    /// </summary>
    public required bool Initialized { get; set; }

    /// <summary>
    /// Cluster name (for display purposes).
    /// </summary>
    public string? ClusterName { get; set; }

    /// <summary>
    /// Cluster ID (unique identifier).
    /// </summary>
    public string? ClusterId { get; set; }

    /// <summary>
    /// Version of the vault.
    /// </summary>
    public string? Version { get; set; }
}
