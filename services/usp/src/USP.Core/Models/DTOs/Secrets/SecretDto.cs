namespace USP.Core.Models.DTOs.Secrets;

/// <summary>
/// Request to create or update a secret (Vault KV v2 compatible)
/// </summary>
public class CreateSecretRequest
{
    /// <summary>
    /// Secret data as key-value pairs
    /// </summary>
    public Dictionary<string, object> Data { get; set; } = new();

    /// <summary>
    /// Optional custom metadata
    /// </summary>
    public Dictionary<string, string>? Options { get; set; }

    /// <summary>
    /// Check-and-Set (CAS) version for optimistic locking (0 = create only)
    /// </summary>
    public int? Cas { get; set; }
}

/// <summary>
/// Response after creating/updating a secret
/// </summary>
public class CreateSecretResponse
{
    public SecretVersionMetadata Data { get; set; } = new();
}

/// <summary>
/// Request to read a secret
/// </summary>
public class ReadSecretRequest
{
    /// <summary>
    /// Specific version to read (null = latest)
    /// </summary>
    public int? Version { get; set; }
}

/// <summary>
/// Response when reading a secret
/// </summary>
public class ReadSecretResponse
{
    public SecretData Data { get; set; } = new();
    public SecretVersionMetadata Metadata { get; set; } = new();
}

/// <summary>
/// Secret data with version info
/// </summary>
public class SecretData
{
    /// <summary>
    /// Decrypted secret data as key-value pairs
    /// </summary>
    public Dictionary<string, object> Data { get; set; } = new();

    /// <summary>
    /// Metadata for this specific version
    /// </summary>
    public SecretVersionInfo Metadata { get; set; } = new();
}

/// <summary>
/// Version-specific metadata
/// </summary>
public class SecretVersionInfo
{
    public int Version { get; set; }
    public DateTime CreatedTime { get; set; }
    public DateTime? DeletionTime { get; set; }
    public bool Destroyed { get; set; }
}

/// <summary>
/// Version metadata returned after create/update
/// </summary>
public class SecretVersionMetadata
{
    public DateTime CreatedTime { get; set; }
    public DateTime? DeletionTime { get; set; }
    public bool Destroyed { get; set; }
    public int Version { get; set; }
}

/// <summary>
/// Complete metadata for a secret path
/// </summary>
public class SecretMetadata
{
    public string Path { get; set; } = string.Empty;
    public DateTime CreatedTime { get; set; }
    public DateTime UpdatedTime { get; set; }
    public int CurrentVersion { get; set; }
    public int OldestVersion { get; set; }
    public int MaxVersions { get; set; } = 10;
    public bool CasRequired { get; set; }
    public DateTime? DeleteVersionAfter { get; set; }
    public Dictionary<string, string> CustomMetadata { get; set; } = new();
    public Dictionary<int, SecretVersionInfo> Versions { get; set; } = new();
}

/// <summary>
/// Request to update secret metadata
/// </summary>
public class UpdateSecretMetadataRequest
{
    public int? MaxVersions { get; set; }
    public bool? CasRequired { get; set; }
    public string? DeleteVersionAfter { get; set; }
    public Dictionary<string, string>? CustomMetadata { get; set; }
}

/// <summary>
/// Request to delete secret versions
/// </summary>
public class DeleteSecretVersionsRequest
{
    public List<int> Versions { get; set; } = new();
}

/// <summary>
/// Request to undelete secret versions
/// </summary>
public class UndeleteSecretVersionsRequest
{
    public List<int> Versions { get; set; } = new();
}

/// <summary>
/// Request to permanently destroy secret versions
/// </summary>
public class DestroySecretVersionsRequest
{
    public List<int> Versions { get; set; } = new();
}

/// <summary>
/// Response when listing secrets
/// </summary>
public class ListSecretsResponse
{
    public ListSecretsData Data { get; set; } = new();
}

/// <summary>
/// List data containing secret keys
/// </summary>
public class ListSecretsData
{
    public List<string> Keys { get; set; } = new();
}
