using USP.Core.Models.DTOs.Database;

namespace USP.Core.Services.Secrets;

/// <summary>
/// Database Engine - Dynamic credential generation and static credential rotation
/// Vault-compatible database secret engine
/// </summary>
public interface IDatabaseEngine
{
    // ============================================
    // Configuration Management
    // ============================================

    /// <summary>
    /// Configure database connection
    /// </summary>
    Task<ConfigureDatabaseResponse> ConfigureDatabaseAsync(string name, ConfigureDatabaseRequest request, Guid userId);

    /// <summary>
    /// Read database configuration
    /// </summary>
    Task<ReadDatabaseConfigResponse?> ReadDatabaseConfigAsync(string name, Guid userId);

    /// <summary>
    /// List all configured databases
    /// </summary>
    Task<ListDatabasesResponse> ListDatabasesAsync(Guid userId);

    /// <summary>
    /// Delete database configuration
    /// </summary>
    Task DeleteDatabaseConfigAsync(string name, Guid userId);

    // ============================================
    // Role Management
    // ============================================

    /// <summary>
    /// Create or update database role
    /// </summary>
    Task<CreateDatabaseRoleResponse> CreateDatabaseRoleAsync(string name, string roleName, CreateDatabaseRoleRequest request, Guid userId);

    /// <summary>
    /// Read database role
    /// </summary>
    Task<ReadDatabaseRoleResponse?> ReadDatabaseRoleAsync(string name, string roleName, Guid userId);

    /// <summary>
    /// List roles for a database
    /// </summary>
    Task<ListDatabaseRolesResponse> ListDatabaseRolesAsync(string name, Guid userId);

    /// <summary>
    /// Delete database role
    /// </summary>
    Task DeleteDatabaseRoleAsync(string name, string roleName, Guid userId);

    // ============================================
    // Dynamic Credential Generation
    // ============================================

    /// <summary>
    /// Generate dynamic credentials for a role
    /// </summary>
    Task<GenerateCredentialsResponse> GenerateCredentialsAsync(string name, string roleName, Guid userId);

    /// <summary>
    /// Revoke dynamic credentials (revoke lease)
    /// </summary>
    Task RevokeDynamicCredentialsAsync(string leaseId, Guid userId);

    // ============================================
    // Static Credential Rotation
    // ============================================

    /// <summary>
    /// Rotate root credentials for a database
    /// </summary>
    Task<RotateRootCredentialsResponse> RotateRootCredentialsAsync(string name, Guid userId);

    /// <summary>
    /// Rotate static credentials for a role
    /// </summary>
    Task<RotateStaticCredentialsResponse> RotateStaticCredentialsAsync(string name, string roleName, Guid userId);

    // ============================================
    // Lease Management
    // ============================================

    /// <summary>
    /// List active leases for a database
    /// </summary>
    Task<ListLeasesResponse> ListLeasesAsync(string name, Guid userId);

    /// <summary>
    /// Renew a lease
    /// </summary>
    Task<RenewLeaseResponse> RenewLeaseAsync(string leaseId, int additionalTtlSeconds, Guid userId);
}
