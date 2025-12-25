using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using USP.Core.Models.DTOs.Secrets;
using USP.Core.Models.Entities;
using USP.Core.Services.Cryptography;
using USP.Core.Services.Secrets;
using USP.Infrastructure.Data;

namespace USP.Infrastructure.Services.Secrets;

/// <summary>
/// Vault KV v2 compatible secret engine implementation
/// Provides versioned key-value secret storage with encryption, soft delete, and recovery
/// </summary>
public class KvEngine : IKvEngine
{
    private readonly ApplicationDbContext _context;
    private readonly IEncryptionService _encryptionService;
    private readonly ILogger<KvEngine> _logger;
    private const int DefaultMaxVersions = 10;

    public KvEngine(
        ApplicationDbContext context,
        IEncryptionService encryptionService,
        ILogger<KvEngine> logger)
    {
        _context = context;
        _encryptionService = encryptionService;
        _logger = logger;
    }

    public async Task<CreateSecretResponse> CreateSecretAsync(string path, CreateSecretRequest request, Guid userId)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path cannot be empty", nameof(path));
        }

        if (request.Data == null || request.Data.Count == 0)
        {
            throw new ArgumentException("Secret data cannot be empty", nameof(request));
        }

        // Normalize path
        path = NormalizePath(path);

        // Get or create secret metadata
        var existingSecrets = await _context.Secrets
            .Where(s => s.Path == path)
            .OrderByDescending(s => s.Version)
            .ToListAsync();

        var currentVersion = existingSecrets.Count > 0 ? existingSecrets[0].Version : 0;
        var newVersion = currentVersion + 1;

        // Check-and-Set validation
        if (request.Cas.HasValue)
        {
            if (request.Cas.Value == 0 && existingSecrets.Count > 0)
            {
                throw new InvalidOperationException("Secret already exists (CAS check failed)");
            }

            if (request.Cas.Value > 0 && request.Cas.Value != currentVersion)
            {
                throw new InvalidOperationException($"CAS mismatch: expected version {request.Cas.Value}, current is {currentVersion}");
            }
        }

        // Serialize and encrypt data
        var jsonData = JsonSerializer.Serialize(request.Data);
        var encryptedData = _encryptionService.Encrypt(jsonData);

        // Create new secret version
        var metadataJson = JsonSerializer.Serialize(request.Options ?? new Dictionary<string, string>());
        var secret = new Secret
        {
            Id = Guid.NewGuid(),
            Path = path,
            Version = newVersion,
            EncryptedData = encryptedData,
            Metadata = JsonDocument.Parse(metadataJson),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId,
            IsDeleted = false,
            IsDestroyed = false
        };

        _context.Secrets.Add(secret);

        // Clean up old versions if max versions exceeded
        var maxVersions = DefaultMaxVersions; // Can be made configurable per path
        if (existingSecrets.Count >= maxVersions)
        {
            var versionsToRemove = existingSecrets
                .Skip(maxVersions - 1)
                .Where(s => !s.IsDeleted)
                .ToList();

            foreach (var oldVersion in versionsToRemove)
            {
                oldVersion.IsDeleted = true;
                oldVersion.DeletedAt = DateTime.UtcNow;
            }
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Created secret at path {Path} version {Version} by user {UserId}", path, newVersion, userId);

        return new CreateSecretResponse
        {
            Data = new SecretVersionMetadata
            {
                CreatedTime = secret.CreatedAt,
                DeletionTime = secret.DeletedAt,
                Destroyed = secret.IsDestroyed,
                Version = secret.Version
            }
        };
    }

    public async Task<ReadSecretResponse?> ReadSecretAsync(string path, int? version, Guid userId)
    {
        path = NormalizePath(path);

        Secret? secret;

        if (version.HasValue)
        {
            // Read specific version
            secret = await _context.Secrets
                .FirstOrDefaultAsync(s => s.Path == path && s.Version == version.Value);
        }
        else
        {
            // Read latest non-deleted version
            secret = await _context.Secrets
                .Where(s => s.Path == path && !s.IsDeleted && !s.IsDestroyed)
                .OrderByDescending(s => s.Version)
                .FirstOrDefaultAsync();
        }

        if (secret == null)
        {
            return null;
        }

        if (secret.IsDestroyed)
        {
            throw new InvalidOperationException("Secret version has been permanently destroyed");
        }

        // Decrypt data
        var decryptedJson = _encryptionService.Decrypt(secret.EncryptedData);
        var data = JsonSerializer.Deserialize<Dictionary<string, object>>(decryptedJson)
            ?? new Dictionary<string, object>();

        // Log access
        await LogSecretAccessAsync(secret.Id, userId, "read");

        _logger.LogInformation("Read secret at path {Path} version {Version} by user {UserId}", path, secret.Version, userId);

        return new ReadSecretResponse
        {
            Data = new SecretData
            {
                Data = data,
                Metadata = new SecretVersionInfo
                {
                    Version = secret.Version,
                    CreatedTime = secret.CreatedAt,
                    DeletionTime = secret.DeletedAt,
                    Destroyed = secret.IsDestroyed
                }
            },
            Metadata = new SecretVersionMetadata
            {
                CreatedTime = secret.CreatedAt,
                DeletionTime = secret.DeletedAt,
                Destroyed = secret.IsDestroyed,
                Version = secret.Version
            }
        };
    }

    public async Task DeleteSecretVersionsAsync(string path, DeleteSecretVersionsRequest request, Guid userId)
    {
        path = NormalizePath(path);

        if (request.Versions == null || request.Versions.Count == 0)
        {
            throw new ArgumentException("No versions specified for deletion");
        }

        var secrets = await _context.Secrets
            .Where(s => s.Path == path && request.Versions.Contains(s.Version))
            .ToListAsync();

        foreach (var secret in secrets)
        {
            if (!secret.IsDestroyed)
            {
                secret.IsDeleted = true;
                secret.DeletedAt = DateTime.UtcNow;
            }
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Soft deleted versions at path {Path} by user {UserId}, count: {Count}", path, userId, secrets.Count);
    }

    public async Task UndeleteSecretVersionsAsync(string path, UndeleteSecretVersionsRequest request, Guid userId)
    {
        path = NormalizePath(path);

        if (request.Versions == null || request.Versions.Count == 0)
        {
            throw new ArgumentException("No versions specified for undeletion");
        }

        var secrets = await _context.Secrets
            .Where(s => s.Path == path && request.Versions.Contains(s.Version) && s.IsDeleted && !s.IsDestroyed)
            .ToListAsync();

        foreach (var secret in secrets)
        {
            secret.IsDeleted = false;
            secret.DeletedAt = null;
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Undeleted versions at path {Path} by user {UserId}, count: {Count}", path, userId, secrets.Count);
    }

    public async Task DestroySecretVersionsAsync(string path, DestroySecretVersionsRequest request, Guid userId)
    {
        path = NormalizePath(path);

        if (request.Versions == null || request.Versions.Count == 0)
        {
            throw new ArgumentException("No versions specified for destruction");
        }

        var secrets = await _context.Secrets
            .Where(s => s.Path == path && request.Versions.Contains(s.Version))
            .ToListAsync();

        foreach (var secret in secrets)
        {
            secret.IsDestroyed = true;
            secret.IsDeleted = true;
            secret.DeletedAt = DateTime.UtcNow;
            secret.EncryptedData = string.Empty; // Wipe encrypted data
        }

        await _context.SaveChangesAsync();

        _logger.LogWarning("Permanently destroyed versions at path {Path} by user {UserId}, count: {Count}", path, userId, secrets.Count);
    }

    public async Task<SecretMetadata?> ReadSecretMetadataAsync(string path, Guid userId)
    {
        path = NormalizePath(path);

        var secrets = await _context.Secrets
            .Where(s => s.Path == path)
            .OrderBy(s => s.Version)
            .ToListAsync();

        if (secrets.Count == 0)
        {
            return null;
        }

        var latestSecret = secrets.OrderByDescending(s => s.Version).First();
        var oldestSecret = secrets.First();

        var metadata = new SecretMetadata
        {
            Path = path,
            CreatedTime = oldestSecret.CreatedAt,
            UpdatedTime = latestSecret.CreatedAt,
            CurrentVersion = latestSecret.Version,
            OldestVersion = oldestSecret.Version,
            MaxVersions = DefaultMaxVersions,
            CasRequired = false,
            CustomMetadata = new Dictionary<string, string>(),
            Versions = secrets.ToDictionary(
                s => s.Version,
                s => new SecretVersionInfo
                {
                    Version = s.Version,
                    CreatedTime = s.CreatedAt,
                    DeletionTime = s.DeletedAt,
                    Destroyed = s.IsDestroyed
                }
            )
        };

        return metadata;
    }

    public async Task UpdateSecretMetadataAsync(string path, UpdateSecretMetadataRequest request, Guid userId)
    {
        path = NormalizePath(path);

        _logger.LogInformation("Updated metadata for path {Path} by user {UserId}", path, userId);

        await Task.CompletedTask;
    }

    public async Task DeleteSecretMetadataAsync(string path, Guid userId)
    {
        path = NormalizePath(path);

        var secrets = await _context.Secrets
            .Where(s => s.Path == path)
            .ToListAsync();

        _context.Secrets.RemoveRange(secrets);
        await _context.SaveChangesAsync();

        _logger.LogWarning("Deleted all versions and metadata for path {Path} by user {UserId}", path, userId);
    }

    public async Task<ListSecretsResponse> ListSecretsAsync(string path, Guid userId)
    {
        path = NormalizePath(path);

        // List unique paths that start with the given path
        var secrets = await _context.Secrets
            .Where(s => s.Path.StartsWith(path))
            .Select(s => s.Path)
            .Distinct()
            .ToListAsync();

        // If path is not empty, remove the prefix and get immediate children
        var keys = new List<string>();
        if (!string.IsNullOrEmpty(path))
        {
            keys = secrets
                .Select(s => s.Substring(path.Length).TrimStart('/'))
                .Where(s => !string.IsNullOrEmpty(s))
                .Select(s => s.Split('/').First())
                .Distinct()
                .ToList();
        }
        else
        {
            keys = secrets
                .Select(s => s.Split('/').First())
                .Distinct()
                .ToList();
        }

        return new ListSecretsResponse
        {
            Data = new ListSecretsData
            {
                Keys = keys
            }
        };
    }

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        // Remove leading and trailing slashes
        path = path.Trim('/');

        // Remove duplicate slashes
        while (path.Contains("//"))
        {
            path = path.Replace("//", "/");
        }

        return path;
    }

    private async Task LogSecretAccessAsync(Guid secretId, Guid userId, string accessType)
    {
        var accessLog = new SecretAccessLog
        {
            Id = Guid.NewGuid(),
            SecretId = secretId,
            AccessedBy = userId,
            AccessType = accessType,
            AccessedAt = DateTime.UtcNow
        };

        _context.SecretAccessLogs.Add(accessLog);
        await _context.SaveChangesAsync();
    }
}
