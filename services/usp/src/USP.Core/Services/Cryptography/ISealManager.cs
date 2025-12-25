using USP.Core.Models.DTOs.Seal;

namespace USP.Core.Services.Cryptography;

/// <summary>
/// Manages seal/unseal operations for the security platform
/// Uses Shamir's Secret Sharing to protect the master encryption key
/// </summary>
public interface ISealManager
{
    /// <summary>
    /// Initialize the seal with Shamir's Secret Sharing
    /// Generates master key and splits it into shares
    /// </summary>
    Task<InitializeSealResponse> InitializeAsync(InitializeSealRequest request);

    /// <summary>
    /// Submit an unseal key share
    /// </summary>
    Task<SealStatusResponse> UnsealAsync(UnsealRequest request);

    /// <summary>
    /// Seal the system (clear master key from memory)
    /// </summary>
    Task<SealResponse> SealAsync();

    /// <summary>
    /// Get current seal status
    /// </summary>
    Task<SealStatusResponse> GetStatusAsync();

    /// <summary>
    /// Check if system is sealed
    /// </summary>
    bool IsSealed();

    /// <summary>
    /// Get the master encryption key (only when unsealed)
    /// </summary>
    byte[]? GetMasterKey();
}
