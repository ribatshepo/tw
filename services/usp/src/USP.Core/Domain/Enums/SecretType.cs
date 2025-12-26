namespace USP.Core.Domain.Enums;

/// <summary>
/// Represents the type of secret stored in the vault
/// </summary>
public enum SecretType
{
    /// <summary>
    /// Generic key-value secret
    /// </summary>
    Generic = 0,

    /// <summary>
    /// Database credentials (username, password, connection string)
    /// </summary>
    Database = 1,

    /// <summary>
    /// TLS/SSL certificate (X.509)
    /// </summary>
    Certificate = 2,

    /// <summary>
    /// SSH private key
    /// </summary>
    SSHKey = 3,

    /// <summary>
    /// API key or token
    /// </summary>
    APIKey = 4,

    /// <summary>
    /// Cloud credentials (AWS, Azure, GCP)
    /// </summary>
    CloudCredential = 5,

    /// <summary>
    /// Encryption key for transit engine
    /// </summary>
    EncryptionKey = 6,

    /// <summary>
    /// OAuth2 client credentials
    /// </summary>
    OAuth2 = 7
}
