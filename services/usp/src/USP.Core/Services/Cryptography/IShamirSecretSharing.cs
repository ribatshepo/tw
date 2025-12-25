namespace USP.Core.Services.Cryptography;

/// <summary>
/// Shamir's Secret Sharing algorithm for splitting and combining secrets
/// Allows splitting a secret into N shares where K shares are needed to reconstruct
/// </summary>
public interface IShamirSecretSharing
{
    /// <summary>
    /// Split a secret into N shares with threshold K
    /// </summary>
    /// <param name="secret">Secret data to split</param>
    /// <param name="threshold">Number of shares needed to reconstruct (K)</param>
    /// <param name="totalShares">Total number of shares to generate (N)</param>
    /// <returns>Array of key shares</returns>
    byte[][] Split(byte[] secret, int threshold, int totalShares);

    /// <summary>
    /// Combine shares to reconstruct the original secret
    /// </summary>
    /// <param name="shares">Array of key shares (must have at least threshold shares)</param>
    /// <returns>Reconstructed secret</returns>
    byte[] Combine(byte[][] shares);

    /// <summary>
    /// Validate that shares can reconstruct a secret
    /// </summary>
    bool ValidateShares(byte[][] shares, int expectedThreshold);
}
