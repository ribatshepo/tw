namespace USP.Core.Interfaces.Services.Secrets;

/// <summary>
/// Provides master encryption key from configured source
/// </summary>
public interface IMasterKeyProvider
{
    /// <summary>
    /// Retrieve the master encryption key (256-bit)
    /// </summary>
    /// <returns>32-byte master key</returns>
    byte[] GetMasterKey();

    /// <summary>
    /// Get the current key version identifier
    /// </summary>
    string GetKeyVersion();

    /// <summary>
    /// Check if key rotation is supported by the current provider
    /// </summary>
    bool SupportsRotation { get; }
}
