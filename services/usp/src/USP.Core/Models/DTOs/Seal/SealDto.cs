namespace USP.Core.Models.DTOs.Seal;

/// <summary>
/// Request to initialize the seal with Shamir's Secret Sharing
/// </summary>
public class InitializeSealRequest
{
    /// <summary>
    /// Number of key shares to generate
    /// </summary>
    public int SecretShares { get; set; } = 5;

    /// <summary>
    /// Number of shares required to unseal (threshold)
    /// </summary>
    public int SecretThreshold { get; set; } = 3;
}

/// <summary>
/// Response after initializing the seal
/// </summary>
public class InitializeSealResponse
{
    /// <summary>
    /// Array of base64-encoded key shares
    /// These must be distributed to different operators
    /// </summary>
    public List<string> Keys { get; set; } = new();

    /// <summary>
    /// Base64-encoded root token for initial access
    /// </summary>
    public string RootToken { get; set; } = string.Empty;

    /// <summary>
    /// Number of key shares generated
    /// </summary>
    public int KeysBase64 { get; set; }

    /// <summary>
    /// Threshold of shares needed to unseal
    /// </summary>
    public int KeysRequired { get; set; }
}

/// <summary>
/// Request to submit an unseal key
/// </summary>
public class UnsealRequest
{
    /// <summary>
    /// Base64-encoded unseal key share
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Optional: reset unseal progress
    /// </summary>
    public bool Reset { get; set; }
}

/// <summary>
/// Response with current seal status
/// </summary>
public class SealStatusResponse
{
    /// <summary>
    /// Whether the system is sealed
    /// </summary>
    public bool Sealed { get; set; }

    /// <summary>
    /// Number of shares required to unseal
    /// </summary>
    public int Threshold { get; set; }

    /// <summary>
    /// Total number of shares
    /// </summary>
    public int TotalShares { get; set; }

    /// <summary>
    /// Number of shares submitted so far
    /// </summary>
    public int Progress { get; set; }

    /// <summary>
    /// Version of the seal configuration
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// Whether the seal has been initialized
    /// </summary>
    public bool Initialized { get; set; }
}

/// <summary>
/// Request to seal the system
/// </summary>
public class SealRequest
{
}

/// <summary>
/// Response after sealing
/// </summary>
public class SealResponse
{
    public bool Sealed { get; set; }
}
