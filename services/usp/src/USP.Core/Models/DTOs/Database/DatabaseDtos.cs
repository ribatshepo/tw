namespace USP.Core.Models.DTOs.Database;

// ============================================
// Configuration DTOs
// ============================================

public class ConfigureDatabaseRequest
{
    public string Plugin { get; set; } = string.Empty; // postgresql, mysql, sqlserver, mongodb, redis, oracle, cassandra, elasticsearch
    public string ConnectionUrl { get; set; } = string.Empty;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public int MaxOpenConnections { get; set; } = 4;
    public int MaxIdleConnections { get; set; } = 2;
    public int MaxConnectionLifetimeSeconds { get; set; } = 3600;
    public Dictionary<string, string>? AdditionalConfig { get; set; }
    public bool VerifyConnection { get; set; } = true;
}

public class ConfigureDatabaseResponse
{
    public string Name { get; set; } = string.Empty;
    public string Plugin { get; set; } = string.Empty;
    public DateTime ConfiguredAt { get; set; }
    public bool ConnectionVerified { get; set; }
}

public class ReadDatabaseConfigResponse
{
    public string Name { get; set; } = string.Empty;
    public string Plugin { get; set; } = string.Empty;
    public string ConnectionUrl { get; set; } = string.Empty;
    public int MaxOpenConnections { get; set; }
    public int MaxIdleConnections { get; set; }
    public int MaxConnectionLifetimeSeconds { get; set; }
    public DateTime ConfiguredAt { get; set; }
    public DateTime? LastRotatedAt { get; set; }
}

public class ListDatabasesResponse
{
    public List<string> Databases { get; set; } = new();
}

// ============================================
// Role DTOs
// ============================================

public class CreateDatabaseRoleRequest
{
    public string CreationStatements { get; set; } = string.Empty;
    public string? RevocationStatements { get; set; }
    public string? RenewStatements { get; set; }
    public string? RollbackStatements { get; set; }
    public int DefaultTtlSeconds { get; set; } = 3600; // 1 hour
    public int MaxTtlSeconds { get; set; } = 86400; // 24 hours
}

public class CreateDatabaseRoleResponse
{
    public string DatabaseName { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class ReadDatabaseRoleResponse
{
    public string DatabaseName { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public string CreationStatements { get; set; } = string.Empty;
    public string? RevocationStatements { get; set; }
    public int DefaultTtlSeconds { get; set; }
    public int MaxTtlSeconds { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ListDatabaseRolesResponse
{
    public List<string> Roles { get; set; } = new();
}

// ============================================
// Credentials DTOs
// ============================================

public class GenerateCredentialsResponse
{
    public string LeaseId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public int LeaseDurationSeconds { get; set; }
    public bool Renewable { get; set; }
}

public class RotateRootCredentialsResponse
{
    public string DatabaseName { get; set; } = string.Empty;
    public DateTime RotatedAt { get; set; }
    public bool Success { get; set; }
    public string? Message { get; set; }
}

public class RotateStaticCredentialsResponse
{
    public string DatabaseName { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public DateTime RotatedAt { get; set; }
    public bool Success { get; set; }
    public string? Message { get; set; }
}

// ============================================
// Lease DTOs
// ============================================

public class ListLeasesResponse
{
    public List<LeaseInfo> Leases { get; set; } = new();
}

public class LeaseInfo
{
    public string LeaseId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsRevoked { get; set; }
}

public class RenewLeaseResponse
{
    public string LeaseId { get; set; } = string.Empty;
    public DateTime NewExpiresAt { get; set; }
    public int LeaseDurationSeconds { get; set; }
}
