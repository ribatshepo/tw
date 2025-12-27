using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using USP.Core.Domain.Entities.Secrets;
using USP.Core.Domain.Enums;
using USP.Core.Interfaces.Services.Secrets;
using USP.Infrastructure.Persistence;

namespace USP.Infrastructure.Services.Secrets;

/// <summary>
/// Implementation of secret service with versioning (Vault KV v2 pattern)
/// </summary>
public class SecretService : ISecretService
{
    private readonly ApplicationDbContext _context;
    private readonly IEncryptionService _encryptionService;
    private readonly ILogger<SecretService> _logger;
    private const string DefaultEncryptionKey = "secret-encryption-key";

    public SecretService(
        ApplicationDbContext context,
        IEncryptionService encryptionService,
        ILogger<SecretService> logger)
    {
        _context = context;
        _encryptionService = encryptionService;
        _logger = logger;
    }

    public async Task<SecretVersion> WriteSecretAsync(
        string path,
        Dictionary<string, string> data,
        int? cas = null,
        string? userId = null,
        CancellationToken cancellationToken = default)
    {
        // Find or create secret
        var secret = await _context.Set<Secret>()
            .Include(s => s.Versions)
            .FirstOrDefaultAsync(s => s.Path == path && !s.IsDeleted, cancellationToken);

        if (secret == null)
        {
            secret = new Secret
            {
                Path = path,
                Type = SecretType.Generic,
                CurrentVersion = 0,
                MaxVersions = 10,
                CasRequired = false,
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userId,
                UpdatedAt = DateTime.UtcNow,
                UpdatedBy = userId
            };

            _context.Set<Secret>().Add(secret);
        }
        else
        {
            // Check-And-Set validation
            if (cas.HasValue && secret.CurrentVersion != cas.Value)
            {
                throw new InvalidOperationException($"CAS mismatch: expected {cas.Value}, got {secret.CurrentVersion}");
            }

            if (secret.CasRequired && !cas.HasValue)
            {
                throw new InvalidOperationException("CAS required but not provided");
            }
        }

        // Increment version
        secret.CurrentVersion++;
        secret.UpdatedAt = DateTime.UtcNow;
        secret.UpdatedBy = userId;

        // Ensure default encryption key exists
        var encryptionKey = await _encryptionService.ReadKeyAsync(DefaultEncryptionKey, cancellationToken);
        if (encryptionKey == null)
        {
            encryptionKey = await _encryptionService.CreateKeyAsync(
                DefaultEncryptionKey,
                EncryptionAlgorithm.AES256GCM,
                exportable: false,
                cancellationToken);
        }

        // Encrypt secret data
        var jsonData = JsonSerializer.Serialize(data);
        var encryptedData = await _encryptionService.EncryptAsync(
            DefaultEncryptionKey,
            jsonData,
            null,
            cancellationToken);

        // Create new version
        var version = new SecretVersion
        {
            SecretId = secret.Id,
            Version = secret.CurrentVersion,
            EncryptedData = encryptedData,
            EncryptionKeyId = encryptionKey.Id,
            IsDeleted = false,
            IsDestroyed = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId
        };

        _context.Set<SecretVersion>().Add(version);

        // Clean up old versions if max versions exceeded
        if (secret.MaxVersions > 0)
        {
            var versionsToDelete = secret.Versions
                .Where(v => !v.IsDestroyed)
                .OrderByDescending(v => v.Version)
                .Skip(secret.MaxVersions)
                .ToList();

            foreach (var oldVersion in versionsToDelete)
            {
                oldVersion.IsDeleted = true;
                oldVersion.DeletedAt = DateTime.UtcNow;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Secret written: {Path} (version {Version})", path, secret.CurrentVersion);

        return version;
    }

    public async Task<Dictionary<string, string>?> ReadSecretAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        var secret = await _context.Set<Secret>()
            .Include(s => s.Versions)
            .FirstOrDefaultAsync(s => s.Path == path && !s.IsDeleted, cancellationToken);

        if (secret == null)
        {
            return null;
        }

        var latestVersion = secret.Versions
            .Where(v => v.IsAccessible())
            .OrderByDescending(v => v.Version)
            .FirstOrDefault();

        if (latestVersion == null)
        {
            return null;
        }

        return await DecryptSecretDataAsync(latestVersion, cancellationToken);
    }

    public async Task<Dictionary<string, string>?> ReadSecretVersionAsync(
        string path,
        int version,
        CancellationToken cancellationToken = default)
    {
        var secret = await _context.Set<Secret>()
            .Include(s => s.Versions)
            .FirstOrDefaultAsync(s => s.Path == path && !s.IsDeleted, cancellationToken);

        if (secret == null)
        {
            return null;
        }

        var secretVersion = secret.Versions
            .FirstOrDefault(v => v.Version == version && v.IsAccessible());

        if (secretVersion == null)
        {
            return null;
        }

        return await DecryptSecretDataAsync(secretVersion, cancellationToken);
    }

    public async Task<List<string>> ListSecretsAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        var normalizedPath = path.TrimEnd('/') + "/";

        return await _context.Set<Secret>()
            .Where(s => s.Path.StartsWith(normalizedPath) && !s.IsDeleted)
            .Select(s => s.Path.Substring(normalizedPath.Length))
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    public async Task DeleteSecretAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        var secret = await _context.Set<Secret>()
            .Include(s => s.Versions)
            .FirstOrDefaultAsync(s => s.Path == path && !s.IsDeleted, cancellationToken);

        if (secret == null)
        {
            throw new InvalidOperationException($"Secret not found: {path}");
        }

        var latestVersion = secret.Versions
            .OrderByDescending(v => v.Version)
            .FirstOrDefault();

        if (latestVersion != null && !latestVersion.IsDeleted)
        {
            latestVersion.IsDeleted = true;
            latestVersion.DeletedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Secret deleted: {Path} (version {Version})", path, latestVersion?.Version);
    }

    public async Task DeleteSecretVersionsAsync(
        string path,
        List<int> versions,
        CancellationToken cancellationToken = default)
    {
        var secret = await _context.Set<Secret>()
            .Include(s => s.Versions)
            .FirstOrDefaultAsync(s => s.Path == path && !s.IsDeleted, cancellationToken);

        if (secret == null)
        {
            throw new InvalidOperationException($"Secret not found: {path}");
        }

        foreach (var versionNumber in versions)
        {
            var version = secret.Versions.FirstOrDefault(v => v.Version == versionNumber);
            if (version != null && !version.IsDeleted)
            {
                version.IsDeleted = true;
                version.DeletedAt = DateTime.UtcNow;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Secret versions deleted: {Path} (versions {Versions})", path, string.Join(", ", versions));
    }

    public async Task UndeleteSecretVersionsAsync(
        string path,
        List<int> versions,
        CancellationToken cancellationToken = default)
    {
        var secret = await _context.Set<Secret>()
            .Include(s => s.Versions)
            .FirstOrDefaultAsync(s => s.Path == path && !s.IsDeleted, cancellationToken);

        if (secret == null)
        {
            throw new InvalidOperationException($"Secret not found: {path}");
        }

        foreach (var versionNumber in versions)
        {
            var version = secret.Versions.FirstOrDefault(v => v.Version == versionNumber);
            if (version != null && version.IsDeleted && !version.IsDestroyed)
            {
                version.IsDeleted = false;
                version.DeletedAt = null;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Secret versions undeleted: {Path} (versions {Versions})", path, string.Join(", ", versions));
    }

    public async Task DestroySecretVersionsAsync(
        string path,
        List<int> versions,
        CancellationToken cancellationToken = default)
    {
        var secret = await _context.Set<Secret>()
            .Include(s => s.Versions)
            .FirstOrDefaultAsync(s => s.Path == path && !s.IsDeleted, cancellationToken);

        if (secret == null)
        {
            throw new InvalidOperationException($"Secret not found: {path}");
        }

        foreach (var versionNumber in versions)
        {
            var version = secret.Versions.FirstOrDefault(v => v.Version == versionNumber);
            if (version != null && !version.IsDestroyed)
            {
                version.IsDestroyed = true;
                version.IsDeleted = true;
                version.DestroyedAt = DateTime.UtcNow;
                version.DeletedAt = DateTime.UtcNow;
                // Clear encrypted data to ensure it can't be recovered
                version.EncryptedData = string.Empty;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogWarning("Secret versions permanently destroyed: {Path} (versions {Versions})", path, string.Join(", ", versions));
    }

    public async Task<Secret?> GetSecretMetadataAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        return await _context.Set<Secret>()
            .Include(s => s.Versions)
            .FirstOrDefaultAsync(s => s.Path == path && !s.IsDeleted, cancellationToken);
    }

    public async Task UpdateSecretMetadataAsync(
        string path,
        int? maxVersions = null,
        bool? casRequired = null,
        CancellationToken cancellationToken = default)
    {
        var secret = await _context.Set<Secret>()
            .FirstOrDefaultAsync(s => s.Path == path && !s.IsDeleted, cancellationToken);

        if (secret == null)
        {
            throw new InvalidOperationException($"Secret not found: {path}");
        }

        if (maxVersions.HasValue)
        {
            secret.MaxVersions = maxVersions.Value;
        }

        if (casRequired.HasValue)
        {
            secret.CasRequired = casRequired.Value;
        }

        secret.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Secret metadata updated: {Path}", path);
    }

    // Private helper methods

    private async Task<Dictionary<string, string>?> DecryptSecretDataAsync(
        SecretVersion version,
        CancellationToken cancellationToken)
    {
        try
        {
            var decryptedJson = await _encryptionService.DecryptAsync(
                DefaultEncryptionKey,
                version.EncryptedData,
                null,
                cancellationToken);

            return JsonSerializer.Deserialize<Dictionary<string, string>>(decryptedJson);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decrypt secret version: {VersionId}", version.Id);
            throw;
        }
    }
}
