namespace USP.Core.Models.Entities;

/// <summary>
/// Database configuration for dynamic credential generation
/// </summary>
public class DatabaseConfig
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Plugin { get; set; } = string.Empty; // postgresql, mysql, mongodb, etc.
    public string EncryptedConnectionUrl { get; set; } = string.Empty;
    public string? EncryptedUsername { get; set; }
    public string? EncryptedPassword { get; set; }
    public int MaxOpenConnections { get; set; }
    public int MaxIdleConnections { get; set; }
    public int MaxConnectionLifetimeSeconds { get; set; }
    public string? AdditionalConfig { get; set; } // JSON string
    public DateTime ConfiguredAt { get; set; }
    public Guid ConfiguredBy { get; set; }
    public DateTime? LastRotatedAt { get; set; }
    public bool IsDeleted { get; set; }

    // Navigation
    public ICollection<DatabaseRole> Roles { get; set; } = new List<DatabaseRole>();
    public ICollection<DatabaseLease> Leases { get; set; } = new List<DatabaseLease>();
}

/// <summary>
/// Database role for generating credentials with specific permissions
/// </summary>
public class DatabaseRole
{
    public Guid Id { get; set; }
    public Guid DatabaseConfigId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public string CreationStatements { get; set; } = string.Empty;
    public string? RevocationStatements { get; set; }
    public string? RenewStatements { get; set; }
    public string? RollbackStatements { get; set; }
    public int DefaultTtlSeconds { get; set; }
    public int MaxTtlSeconds { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    // Navigation
    public DatabaseConfig DatabaseConfig { get; set; } = null!;
    public ICollection<DatabaseLease> Leases { get; set; } = new List<DatabaseLease>();
}

/// <summary>
/// Lease for dynamically generated database credentials
/// </summary>
public class DatabaseLease
{
    public Guid Id { get; set; }
    public string LeaseId { get; set; } = string.Empty; // Unique lease identifier
    public Guid DatabaseConfigId { get; set; }
    public Guid DatabaseRoleId { get; set; }
    public string GeneratedUsername { get; set; } = string.Empty;
    public string EncryptedPassword { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public bool IsRevoked { get; set; }
    public int RenewalCount { get; set; }

    // Navigation
    public DatabaseConfig DatabaseConfig { get; set; } = null!;
    public DatabaseRole DatabaseRole { get; set; } = null!;
}
