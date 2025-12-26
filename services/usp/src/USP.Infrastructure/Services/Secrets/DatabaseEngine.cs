using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using USP.Core.Models.DTOs.Database;
using USP.Core.Models.Entities;
using USP.Core.Services.Cryptography;
using USP.Core.Services.Secrets;
using USP.Infrastructure.Data;

namespace USP.Infrastructure.Services.Secrets;

/// <summary>
/// Database Engine - Dynamic credential generation and static credential rotation
/// Vault-compatible database secret engine with support for 8 database types
/// </summary>
public class DatabaseEngine : IDatabaseEngine
{
    private readonly ApplicationDbContext _context;
    private readonly IEncryptionService _encryptionService;
    private readonly ILogger<DatabaseEngine> _logger;
    private readonly Dictionary<string, IDatabaseConnector> _connectors;

    public DatabaseEngine(
        ApplicationDbContext context,
        IEncryptionService encryptionService,
        ILogger<DatabaseEngine> logger,
        IEnumerable<IDatabaseConnector> connectors)
    {
        _context = context;
        _encryptionService = encryptionService;
        _logger = logger;

        // Register all connectors by plugin name
        _connectors = connectors.ToDictionary(c => c.PluginName, c => c);
        _logger.LogInformation("Registered {Count} database connectors: {Plugins}",
            _connectors.Count,
            string.Join(", ", _connectors.Keys));
    }

    // ============================================
    // Configuration Management
    // ============================================

    public async Task<ConfigureDatabaseResponse> ConfigureDatabaseAsync(string name, ConfigureDatabaseRequest request, Guid userId)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Database name cannot be empty", nameof(name));
        }

        if (!_connectors.ContainsKey(request.Plugin))
        {
            throw new ArgumentException(
                $"Unsupported database plugin: {request.Plugin}. Supported: {string.Join(", ", _connectors.Keys)}");
        }

        // Check if database configuration already exists
        var existingConfig = await _context.DatabaseConfigs
            .FirstOrDefaultAsync(d => d.Name == name && !d.IsDeleted);

        var connector = _connectors[request.Plugin];

        // Verify connection if requested
        var connectionVerified = false;
        if (request.VerifyConnection)
        {
            connectionVerified = await connector.VerifyConnectionAsync(
                request.ConnectionUrl,
                request.Username,
                request.Password);

            if (!connectionVerified)
            {
                throw new InvalidOperationException($"Failed to verify connection to {request.Plugin} database");
            }
        }

        // Encrypt sensitive data
        var encryptedConnectionUrl = _encryptionService.Encrypt(request.ConnectionUrl);
        var encryptedUsername = request.Username != null ? _encryptionService.Encrypt(request.Username) : null;
        var encryptedPassword = request.Password != null ? _encryptionService.Encrypt(request.Password) : null;

        var additionalConfigJson = request.AdditionalConfig != null
            ? JsonSerializer.Serialize(request.AdditionalConfig)
            : null;

        if (existingConfig != null)
        {
            // Update existing configuration
            existingConfig.Plugin = request.Plugin;
            existingConfig.EncryptedConnectionUrl = encryptedConnectionUrl;
            existingConfig.EncryptedUsername = encryptedUsername;
            existingConfig.EncryptedPassword = encryptedPassword;
            existingConfig.MaxOpenConnections = request.MaxOpenConnections;
            existingConfig.MaxIdleConnections = request.MaxIdleConnections;
            existingConfig.MaxConnectionLifetimeSeconds = request.MaxConnectionLifetimeSeconds;
            existingConfig.AdditionalConfig = additionalConfigJson;
            existingConfig.ConfiguredAt = DateTime.UtcNow;
            existingConfig.ConfiguredBy = userId;
        }
        else
        {
            // Create new configuration
            var dbConfig = new DatabaseConfig
            {
                Id = Guid.NewGuid(),
                Name = name,
                Plugin = request.Plugin,
                EncryptedConnectionUrl = encryptedConnectionUrl,
                EncryptedUsername = encryptedUsername,
                EncryptedPassword = encryptedPassword,
                MaxOpenConnections = request.MaxOpenConnections,
                MaxIdleConnections = request.MaxIdleConnections,
                MaxConnectionLifetimeSeconds = request.MaxConnectionLifetimeSeconds,
                AdditionalConfig = additionalConfigJson,
                ConfiguredAt = DateTime.UtcNow,
                ConfiguredBy = userId,
                IsDeleted = false
            };

            _context.DatabaseConfigs.Add(dbConfig);
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Configured database '{Name}' with plugin '{Plugin}' by user {UserId}",
            name, request.Plugin, userId);

        return new ConfigureDatabaseResponse
        {
            Name = name,
            Plugin = request.Plugin,
            ConfiguredAt = DateTime.UtcNow,
            ConnectionVerified = connectionVerified
        };
    }

    public async Task<ReadDatabaseConfigResponse?> ReadDatabaseConfigAsync(string name, Guid userId)
    {
        var config = await _context.DatabaseConfigs
            .FirstOrDefaultAsync(d => d.Name == name && !d.IsDeleted);

        if (config == null)
        {
            return null;
        }

        // Decrypt connection URL (but don't return credentials)
        var connectionUrl = _encryptionService.Decrypt(config.EncryptedConnectionUrl);

        return new ReadDatabaseConfigResponse
        {
            Name = config.Name,
            Plugin = config.Plugin,
            ConnectionUrl = MaskConnectionUrl(connectionUrl),
            MaxOpenConnections = config.MaxOpenConnections,
            MaxIdleConnections = config.MaxIdleConnections,
            MaxConnectionLifetimeSeconds = config.MaxConnectionLifetimeSeconds,
            ConfiguredAt = config.ConfiguredAt,
            LastRotatedAt = config.LastRotatedAt
        };
    }

    public async Task<ListDatabasesResponse> ListDatabasesAsync(Guid userId)
    {
        var databases = await _context.DatabaseConfigs
            .Where(d => !d.IsDeleted)
            .OrderBy(d => d.Name)
            .Select(d => d.Name)
            .ToListAsync();

        return new ListDatabasesResponse { Databases = databases };
    }

    public async Task DeleteDatabaseConfigAsync(string name, Guid userId)
    {
        var config = await _context.DatabaseConfigs
            .Include(d => d.Roles)
            .Include(d => d.Leases)
            .FirstOrDefaultAsync(d => d.Name == name && !d.IsDeleted)
            ?? throw new KeyNotFoundException($"Database configuration '{name}' not found");

        // Revoke all active leases first
        var activeLeases = config.Leases.Where(l => !l.IsRevoked).ToList();
        foreach (var lease in activeLeases)
        {
            await RevokeDynamicCredentialsAsync(lease.LeaseId, userId);
        }

        // Soft delete configuration and related roles
        config.IsDeleted = true;
        foreach (var role in config.Roles)
        {
            role.IsDeleted = true;
        }

        await _context.SaveChangesAsync();

        _logger.LogWarning("Deleted database configuration '{Name}' by user {UserId}", name, userId);
    }

    // ============================================
    // Role Management
    // ============================================

    public async Task<CreateDatabaseRoleResponse> CreateDatabaseRoleAsync(
        string name,
        string roleName,
        CreateDatabaseRoleRequest request,
        Guid userId)
    {
        var config = await _context.DatabaseConfigs
            .Include(d => d.Roles)
            .FirstOrDefaultAsync(d => d.Name == name && !d.IsDeleted)
            ?? throw new KeyNotFoundException($"Database configuration '{name}' not found");

        if (string.IsNullOrWhiteSpace(request.CreationStatements))
        {
            throw new ArgumentException("Creation statements cannot be empty", nameof(request.CreationStatements));
        }

        if (request.DefaultTtlSeconds < 60 || request.DefaultTtlSeconds > 86400 * 30)
        {
            throw new ArgumentException("DefaultTtlSeconds must be between 60 and 2,592,000 (30 days)");
        }

        // Check if role already exists
        var existingRole = config.Roles.FirstOrDefault(r => r.RoleName == roleName && !r.IsDeleted);

        if (existingRole != null)
        {
            // Update existing role
            existingRole.CreationStatements = request.CreationStatements;
            existingRole.RevocationStatements = request.RevocationStatements;
            existingRole.RenewStatements = request.RenewStatements;
            existingRole.RollbackStatements = request.RollbackStatements;
            existingRole.DefaultTtlSeconds = request.DefaultTtlSeconds;
            existingRole.MaxTtlSeconds = request.MaxTtlSeconds;
            existingRole.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            // Create new role
            var dbRole = new DatabaseRole
            {
                Id = Guid.NewGuid(),
                DatabaseConfigId = config.Id,
                RoleName = roleName,
                CreationStatements = request.CreationStatements,
                RevocationStatements = request.RevocationStatements,
                RenewStatements = request.RenewStatements,
                RollbackStatements = request.RollbackStatements,
                DefaultTtlSeconds = request.DefaultTtlSeconds,
                MaxTtlSeconds = request.MaxTtlSeconds,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userId,
                IsDeleted = false
            };

            _context.DatabaseRoles.Add(dbRole);
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Created/Updated database role '{RoleName}' for database '{DatabaseName}' by user {UserId}",
            roleName, name, userId);

        return new CreateDatabaseRoleResponse
        {
            DatabaseName = name,
            RoleName = roleName,
            CreatedAt = DateTime.UtcNow
        };
    }

    public async Task<ReadDatabaseRoleResponse?> ReadDatabaseRoleAsync(string name, string roleName, Guid userId)
    {
        var role = await _context.DatabaseRoles
            .Include(r => r.DatabaseConfig)
            .FirstOrDefaultAsync(r =>
                r.DatabaseConfig.Name == name &&
                r.RoleName == roleName &&
                !r.IsDeleted &&
                !r.DatabaseConfig.IsDeleted);

        if (role == null)
        {
            return null;
        }

        return new ReadDatabaseRoleResponse
        {
            DatabaseName = name,
            RoleName = role.RoleName,
            CreationStatements = role.CreationStatements,
            RevocationStatements = role.RevocationStatements,
            DefaultTtlSeconds = role.DefaultTtlSeconds,
            MaxTtlSeconds = role.MaxTtlSeconds,
            CreatedAt = role.CreatedAt
        };
    }

    public async Task<ListDatabaseRolesResponse> ListDatabaseRolesAsync(string name, Guid userId)
    {
        var roles = await _context.DatabaseRoles
            .Include(r => r.DatabaseConfig)
            .Where(r => r.DatabaseConfig.Name == name && !r.IsDeleted && !r.DatabaseConfig.IsDeleted)
            .OrderBy(r => r.RoleName)
            .Select(r => r.RoleName)
            .ToListAsync();

        return new ListDatabaseRolesResponse { Roles = roles };
    }

    public async Task DeleteDatabaseRoleAsync(string name, string roleName, Guid userId)
    {
        var role = await _context.DatabaseRoles
            .Include(r => r.DatabaseConfig)
            .Include(r => r.Leases)
            .FirstOrDefaultAsync(r =>
                r.DatabaseConfig.Name == name &&
                r.RoleName == roleName &&
                !r.IsDeleted)
            ?? throw new KeyNotFoundException($"Database role '{roleName}' not found for database '{name}'");

        // Revoke all active leases for this role
        var activeLeases = role.Leases.Where(l => !l.IsRevoked).ToList();
        foreach (var lease in activeLeases)
        {
            await RevokeDynamicCredentialsAsync(lease.LeaseId, userId);
        }

        // Soft delete role
        role.IsDeleted = true;
        await _context.SaveChangesAsync();

        _logger.LogWarning("Deleted database role '{RoleName}' for database '{DatabaseName}' by user {UserId}",
            roleName, name, userId);
    }

    // ============================================
    // Dynamic Credential Generation
    // ============================================

    public async Task<GenerateCredentialsResponse> GenerateCredentialsAsync(string name, string roleName, Guid userId)
    {
        var role = await _context.DatabaseRoles
            .Include(r => r.DatabaseConfig)
            .FirstOrDefaultAsync(r =>
                r.DatabaseConfig.Name == name &&
                r.RoleName == roleName &&
                !r.IsDeleted &&
                !r.DatabaseConfig.IsDeleted)
            ?? throw new KeyNotFoundException($"Database role '{roleName}' not found for database '{name}'");

        var config = role.DatabaseConfig;
        var connector = _connectors[config.Plugin];

        // Decrypt admin credentials
        var connectionUrl = _encryptionService.Decrypt(config.EncryptedConnectionUrl);
        var adminUsername = config.EncryptedUsername != null ? _encryptionService.Decrypt(config.EncryptedUsername) : null;
        var adminPassword = config.EncryptedPassword != null ? _encryptionService.Decrypt(config.EncryptedPassword) : null;

        // Generate dynamic user
        var (username, password) = await connector.CreateDynamicUserAsync(
            connectionUrl,
            adminUsername!,
            adminPassword!,
            role.CreationStatements,
            role.DefaultTtlSeconds);

        // Create lease
        var leaseId = $"database/{name}/{roleName}/{Guid.NewGuid()}";
        var expiresAt = DateTime.UtcNow.AddSeconds(role.DefaultTtlSeconds);

        var lease = new DatabaseLease
        {
            Id = Guid.NewGuid(),
            LeaseId = leaseId,
            DatabaseConfigId = config.Id,
            DatabaseRoleId = role.Id,
            GeneratedUsername = username,
            EncryptedPassword = _encryptionService.Encrypt(password),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId,
            ExpiresAt = expiresAt,
            IsRevoked = false,
            RenewalCount = 0
        };

        _context.DatabaseLeases.Add(lease);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Generated dynamic credentials for database '{Database}' role '{Role}', username: {Username}, lease: {LeaseId}, expires: {ExpiresAt}",
            name, roleName, username, leaseId, expiresAt);

        return new GenerateCredentialsResponse
        {
            LeaseId = leaseId,
            Username = username,
            Password = password,
            ExpiresAt = expiresAt,
            LeaseDurationSeconds = role.DefaultTtlSeconds,
            Renewable = true
        };
    }

    public async Task RevokeDynamicCredentialsAsync(string leaseId, Guid userId)
    {
        var lease = await _context.DatabaseLeases
            .Include(l => l.DatabaseConfig)
            .Include(l => l.DatabaseRole)
            .FirstOrDefaultAsync(l => l.LeaseId == leaseId && !l.IsRevoked)
            ?? throw new KeyNotFoundException($"Lease '{leaseId}' not found or already revoked");

        var config = lease.DatabaseConfig;
        var role = lease.DatabaseRole;
        var connector = _connectors[config.Plugin];

        // Decrypt admin credentials
        var connectionUrl = _encryptionService.Decrypt(config.EncryptedConnectionUrl);
        var adminUsername = config.EncryptedUsername != null ? _encryptionService.Decrypt(config.EncryptedUsername) : null;
        var adminPassword = config.EncryptedPassword != null ? _encryptionService.Decrypt(config.EncryptedPassword) : null;

        // Revoke user from database
        var revoked = await connector.RevokeDynamicUserAsync(
            connectionUrl,
            adminUsername!,
            adminPassword!,
            lease.GeneratedUsername,
            role.RevocationStatements);

        if (revoked)
        {
            lease.IsRevoked = true;
            lease.RevokedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Revoked dynamic credentials lease: {LeaseId}, username: {Username}",
                leaseId, lease.GeneratedUsername);
        }
        else
        {
            _logger.LogWarning("Failed to revoke dynamic credentials lease: {LeaseId}, marking as revoked anyway",
                leaseId);
            lease.IsRevoked = true;
            lease.RevokedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    // ============================================
    // Static Credential Rotation
    // ============================================

    public async Task<RotateRootCredentialsResponse> RotateRootCredentialsAsync(string name, Guid userId)
    {
        var config = await _context.DatabaseConfigs
            .FirstOrDefaultAsync(d => d.Name == name && !d.IsDeleted)
            ?? throw new KeyNotFoundException($"Database configuration '{name}' not found");

        var connector = _connectors[config.Plugin];

        // Decrypt current credentials
        var connectionUrl = _encryptionService.Decrypt(config.EncryptedConnectionUrl);
        var currentUsername = config.EncryptedUsername != null ? _encryptionService.Decrypt(config.EncryptedUsername) : null;
        var currentPassword = config.EncryptedPassword != null ? _encryptionService.Decrypt(config.EncryptedPassword) : null;

        if (string.IsNullOrEmpty(currentUsername) || string.IsNullOrEmpty(currentPassword))
        {
            throw new InvalidOperationException("Root credentials not configured for this database");
        }

        // Generate new password
        var newPassword = connector.GeneratePassword();

        // Rotate credentials
        var rotatedPassword = await connector.RotateRootCredentialsAsync(
            connectionUrl,
            currentUsername,
            currentPassword,
            newPassword);

        // Update stored credentials
        config.EncryptedPassword = _encryptionService.Encrypt(rotatedPassword);
        config.LastRotatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Rotated root credentials for database '{Database}' by user {UserId}", name, userId);

        return new RotateRootCredentialsResponse
        {
            DatabaseName = name,
            RotatedAt = DateTime.UtcNow,
            Success = true,
            Message = "Root credentials rotated successfully"
        };
    }

    public Task<RotateStaticCredentialsResponse> RotateStaticCredentialsAsync(string name, string roleName, Guid userId)
    {
        _logger.LogWarning(
            "Static credential rotation requested for database '{DatabaseName}' role '{RoleName}' by user {UserId}, but this feature is not supported",
            name, roleName, userId);

        throw new NotSupportedException(
            "Static credential rotation is not currently supported. " +
            "Use dynamic credentials with automatic expiration (GenerateCredentialsAsync) instead. " +
            "Dynamic credentials are automatically created, rotated, and revoked based on TTL settings. " +
            "Static credential rotation requires database-specific password change plugins and will be available in a future release.");
    }

    // ============================================
    // Lease Management
    // ============================================

    public async Task<ListLeasesResponse> ListLeasesAsync(string name, Guid userId)
    {
        var leases = await _context.DatabaseLeases
            .Include(l => l.DatabaseConfig)
            .Include(l => l.DatabaseRole)
            .Where(l => l.DatabaseConfig.Name == name && !l.IsRevoked)
            .OrderByDescending(l => l.CreatedAt)
            .Select(l => new LeaseInfo
            {
                LeaseId = l.LeaseId,
                Username = l.GeneratedUsername,
                RoleName = l.DatabaseRole.RoleName,
                CreatedAt = l.CreatedAt,
                ExpiresAt = l.ExpiresAt,
                IsRevoked = l.IsRevoked
            })
            .ToListAsync();

        return new ListLeasesResponse { Leases = leases };
    }

    public async Task<RenewLeaseResponse> RenewLeaseAsync(string leaseId, int additionalTtlSeconds, Guid userId)
    {
        var lease = await _context.DatabaseLeases
            .Include(l => l.DatabaseRole)
            .FirstOrDefaultAsync(l => l.LeaseId == leaseId && !l.IsRevoked)
            ?? throw new KeyNotFoundException($"Lease '{leaseId}' not found or already revoked");

        if (DateTime.UtcNow > lease.ExpiresAt)
        {
            throw new InvalidOperationException("Lease has already expired and cannot be renewed");
        }

        var role = lease.DatabaseRole;

        // Calculate new expiration
        var totalTtl = (int)(lease.ExpiresAt - DateTime.UtcNow).TotalSeconds + additionalTtlSeconds;
        if (totalTtl > role.MaxTtlSeconds)
        {
            throw new InvalidOperationException(
                $"Renewal would exceed max TTL of {role.MaxTtlSeconds} seconds for this role");
        }

        var newExpiresAt = lease.ExpiresAt.AddSeconds(additionalTtlSeconds);
        lease.ExpiresAt = newExpiresAt;
        lease.RenewalCount++;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Renewed lease {LeaseId} with additional {AdditionalTtl} seconds, new expiration: {NewExpiration}",
            leaseId, additionalTtlSeconds, newExpiresAt);

        return new RenewLeaseResponse
        {
            LeaseId = leaseId,
            NewExpiresAt = newExpiresAt,
            LeaseDurationSeconds = (int)(newExpiresAt - DateTime.UtcNow).TotalSeconds
        };
    }

    // ============================================
    // Helper Methods
    // ============================================

    private string MaskConnectionUrl(string connectionUrl)
    {
        // Mask passwords in connection URLs
        var patterns = new[]
        {
            (@"password=([^;]+)", "password=***"),
            (@"pwd=([^;]+)", "pwd=***"),
            (@":([^@]+)@", ":***@")
        };

        var masked = connectionUrl;
        foreach (var (pattern, replacement) in patterns)
        {
            masked = System.Text.RegularExpressions.Regex.Replace(
                masked,
                pattern,
                replacement,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        return masked;
    }
}
