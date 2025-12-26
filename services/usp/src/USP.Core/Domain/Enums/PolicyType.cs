namespace USP.Core.Domain.Enums;

/// <summary>
/// Represents the type of access control policy
/// </summary>
public enum PolicyType
{
    /// <summary>
    /// Role-Based Access Control policy
    /// </summary>
    RBAC = 0,

    /// <summary>
    /// Attribute-Based Access Control policy (JSON format)
    /// </summary>
    ABAC = 1,

    /// <summary>
    /// HashiCorp Configuration Language policy (Vault-compatible)
    /// </summary>
    HCL = 2
}
