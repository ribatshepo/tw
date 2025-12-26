namespace USP.Core.Domain.Enums;

/// <summary>
/// Represents the type of credential rotation
/// </summary>
public enum RotationType
{
    /// <summary>
    /// Database credential rotation (PostgreSQL, MySQL, SQL Server, MongoDB, Redis)
    /// </summary>
    Database = 0,

    /// <summary>
    /// SSH key rotation (Linux/Unix servers)
    /// </summary>
    SSHKey = 1,

    /// <summary>
    /// API key rotation (service accounts)
    /// </summary>
    APIKey = 2,

    /// <summary>
    /// Certificate rotation (TLS/SSL, client certificates)
    /// </summary>
    Certificate = 3,

    /// <summary>
    /// Cloud credential rotation (AWS IAM, Azure Service Principals, GCP Service Accounts)
    /// </summary>
    CloudCredential = 4
}
