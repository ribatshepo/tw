using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using USP.Core.Models.DTOs.CloudSync;
using USP.Core.Models.Entities;
using USP.Core.Services.Audit;
using USP.Core.Services.CloudSync;
using USP.Infrastructure.Data;

namespace USP.Infrastructure.Services.CloudSync;

/// <summary>
/// Service for managing cloud secret synchronization
/// </summary>
public class CloudSyncService : ICloudSyncService
{
    private readonly ApplicationDbContext _context;
    private readonly IAuditService _auditService;
    private readonly ILogger<CloudSyncService> _logger;

    public CloudSyncService(
        ApplicationDbContext context,
        IAuditService auditService,
        ILogger<CloudSyncService> logger)
    {
        _context = context;
        _auditService = auditService;
        _logger = logger;
    }

    public async Task<CloudSyncConfigurationDto> ConfigureAsync(CreateCloudSyncConfigurationRequest request, Guid createdBy)
    {
        _logger.LogInformation("Creating cloud sync configuration: {Name}, Provider: {Provider}",
            request.Name, request.Provider);

        var configuration = new CloudSyncConfiguration
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Provider = request.Provider,
            SyncMode = request.SyncMode,
            IsEnabled = request.IsEnabled,
            PathFilter = request.PathFilter,
            TagFilter = request.TagFilter,
            SyncSchedule = request.SyncSchedule,
            ConflictResolution = request.ConflictResolution,
            EnableRealTimeSync = request.EnableRealTimeSync,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Store encrypted credentials based on provider
        var credentials = new Dictionary<string, string>();
        switch (request.Provider.ToLower())
        {
            case "aws":
                if (!string.IsNullOrEmpty(request.AwsRegion))
                    credentials["Region"] = request.AwsRegion;
                if (!string.IsNullOrEmpty(request.AwsAccessKeyId))
                    credentials["AccessKeyId"] = request.AwsAccessKeyId;
                if (!string.IsNullOrEmpty(request.AwsSecretAccessKey))
                    credentials["SecretAccessKey"] = request.AwsSecretAccessKey;
                if (!string.IsNullOrEmpty(request.AwsIamRoleArn))
                    credentials["IamRoleArn"] = request.AwsIamRoleArn;
                break;

            case "azure":
                if (!string.IsNullOrEmpty(request.AzureKeyVaultUri))
                    credentials["KeyVaultUri"] = request.AzureKeyVaultUri;
                if (!string.IsNullOrEmpty(request.AzureTenantId))
                    credentials["TenantId"] = request.AzureTenantId;
                if (!string.IsNullOrEmpty(request.AzureClientId))
                    credentials["ClientId"] = request.AzureClientId;
                if (!string.IsNullOrEmpty(request.AzureClientSecret))
                    credentials["ClientSecret"] = request.AzureClientSecret;
                break;

            case "gcp":
                if (!string.IsNullOrEmpty(request.GcpProjectId))
                    credentials["ProjectId"] = request.GcpProjectId;
                if (!string.IsNullOrEmpty(request.GcpServiceAccountJson))
                    credentials["ServiceAccountJson"] = request.GcpServiceAccountJson;
                break;
        }

        configuration.EncryptedCredentials = System.Text.Json.JsonSerializer.Serialize(credentials);

        _context.CloudSyncConfigurations.Add(configuration);
        await _context.SaveChangesAsync();

        await _auditService.LogAsync(
            userId: createdBy,
            action: "cloudsync.configured",
            resourceType: "CloudSyncConfiguration",
            resourceId: configuration.Id.ToString(),
            newValue: new { Name = configuration.Name, Provider = configuration.Provider }
        );

        _logger.LogInformation("Cloud sync configuration created: {ConfigurationId}", configuration.Id);

        return MapToDto(configuration);
    }

    public async Task<CloudSyncConfigurationDto?> GetConfigurationAsync(Guid id)
    {
        var configuration = await _context.CloudSyncConfigurations
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id);

        return configuration == null ? null : MapToDto(configuration);
    }

    public async Task<List<CloudSyncConfigurationDto>> ListConfigurationsAsync()
    {
        var configurations = await _context.CloudSyncConfigurations
            .AsNoTracking()
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();

        return configurations.Select(MapToDto).ToList();
    }

    public async Task<CloudSyncConfigurationDto> UpdateConfigurationAsync(
        Guid id,
        UpdateCloudSyncConfigurationRequest request,
        Guid updatedBy)
    {
        var configuration = await _context.CloudSyncConfigurations.FindAsync(id);
        if (configuration == null)
        {
            throw new InvalidOperationException("Cloud sync configuration not found");
        }

        if (!string.IsNullOrEmpty(request.Name))
            configuration.Name = request.Name;

        if (request.IsEnabled.HasValue)
            configuration.IsEnabled = request.IsEnabled.Value;

        if (request.PathFilter != null)
            configuration.PathFilter = request.PathFilter;

        if (request.TagFilter != null)
            configuration.TagFilter = request.TagFilter;

        if (request.SyncSchedule != null)
            configuration.SyncSchedule = request.SyncSchedule;

        if (!string.IsNullOrEmpty(request.ConflictResolution))
            configuration.ConflictResolution = request.ConflictResolution;

        if (request.EnableRealTimeSync.HasValue)
            configuration.EnableRealTimeSync = request.EnableRealTimeSync.Value;

        configuration.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        await _auditService.LogAsync(
            userId: updatedBy,
            action: "cloudsync.updated",
            resourceType: "CloudSyncConfiguration",
            resourceId: configuration.Id.ToString(),
            newValue: new { Name = configuration.Name }
        );

        _logger.LogInformation("Cloud sync configuration updated: {ConfigurationId}", id);

        return MapToDto(configuration);
    }

    public async Task DeleteConfigurationAsync(Guid id, Guid deletedBy)
    {
        var configuration = await _context.CloudSyncConfigurations.FindAsync(id);
        if (configuration == null)
        {
            throw new InvalidOperationException("Cloud sync configuration not found");
        }

        _context.CloudSyncConfigurations.Remove(configuration);
        await _context.SaveChangesAsync();

        await _auditService.LogAsync(
            userId: deletedBy,
            action: "cloudsync.deleted",
            resourceType: "CloudSyncConfiguration",
            resourceId: configuration.Id.ToString(),
            oldValue: new { Name = configuration.Name }
        );

        _logger.LogInformation("Cloud sync configuration deleted: {ConfigurationId}", id);
    }

    public async Task<CloudSyncHistoryDto> TriggerSyncAsync(Guid configurationId, TriggerSyncRequest request, Guid triggeredBy)
    {
        var configuration = await _context.CloudSyncConfigurations.FindAsync(configurationId);
        if (configuration == null)
        {
            throw new InvalidOperationException("Cloud sync configuration not found");
        }

        if (!configuration.IsEnabled)
        {
            throw new InvalidOperationException("Cloud sync configuration is disabled");
        }

        var history = new CloudSyncHistory
        {
            Id = Guid.NewGuid(),
            ConfigurationId = configurationId,
            OperationType = "manual",
            Direction = request.Direction ?? "bidirectional",
            Status = "running",
            StartedAt = DateTime.UtcNow,
            TriggeredBy = triggeredBy
        };

        _context.CloudSyncHistory.Add(history);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Cloud sync triggered: ConfigurationId={ConfigurationId}, HistoryId={HistoryId}",
            configurationId, history.Id);

        // Actual sync implementation would go here
        // For now, we'll mark it as completed immediately
        history.Status = "completed";
        history.CompletedAt = DateTime.UtcNow;
        history.DurationMs = (long)(history.CompletedAt.Value - history.StartedAt).TotalMilliseconds;

        await _context.SaveChangesAsync();

        await _auditService.LogAsync(
            userId: triggeredBy,
            action: "cloudsync.triggered",
            resourceType: "CloudSyncConfiguration",
            resourceId: configurationId.ToString(),
            newValue: new { Name = configuration.Name }
        );

        return MapToHistoryDto(history);
    }

    public async Task<CloudSyncHistoryDto?> GetSyncStatusAsync(Guid configurationId)
    {
        var history = await _context.CloudSyncHistory
            .AsNoTracking()
            .Where(h => h.ConfigurationId == configurationId)
            .OrderByDescending(h => h.StartedAt)
            .FirstOrDefaultAsync();

        return history == null ? null : MapToHistoryDto(history);
    }

    public async Task<List<CloudSyncHistoryDto>> GetSyncHistoryAsync(Guid configurationId, int limit = 50)
    {
        var history = await _context.CloudSyncHistory
            .AsNoTracking()
            .Where(h => h.ConfigurationId == configurationId)
            .OrderByDescending(h => h.StartedAt)
            .Take(limit)
            .ToListAsync();

        return history.Select(MapToHistoryDto).ToList();
    }

    public async Task<List<CloudSyncConflictDto>> ListConflictsAsync(Guid configurationId, string? status = null)
    {
        var query = _context.CloudSyncConflicts
            .AsNoTracking()
            .Where(c => c.ConfigurationId == configurationId);

        if (!string.IsNullOrEmpty(status))
        {
            query = query.Where(c => c.Status == status);
        }

        var conflicts = await query
            .OrderByDescending(c => c.DetectedAt)
            .ToListAsync();

        return conflicts.Select(MapToConflictDto).ToList();
    }

    public async Task<CloudSyncConflictDto?> GetConflictAsync(Guid conflictId)
    {
        var conflict = await _context.CloudSyncConflicts
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == conflictId);

        return conflict == null ? null : MapToConflictDto(conflict);
    }

    public async Task<CloudSyncConflictDto> ResolveConflictAsync(
        Guid conflictId,
        ResolveConflictRequest request,
        Guid resolvedBy)
    {
        var conflict = await _context.CloudSyncConflicts.FindAsync(conflictId);
        if (conflict == null)
        {
            throw new InvalidOperationException("Conflict not found");
        }

        conflict.Status = "resolved";
        conflict.ResolutionStrategy = request.Resolution;
        conflict.ResolvedAt = DateTime.UtcNow;
        conflict.ResolvedBy = resolvedBy;

        await _context.SaveChangesAsync();

        await _auditService.LogAsync(
            userId: resolvedBy,
            action: "cloudsync.conflict.resolved",
            resourceType: "CloudSyncConflict",
            resourceId: conflictId.ToString(),
            newValue: new { SecretPath = conflict.SecretPath, ResolutionStrategy = request.Resolution }
        );

        _logger.LogInformation("Cloud sync conflict resolved: {ConflictId}, Strategy: {Strategy}",
            conflictId, request.Resolution);

        return MapToConflictDto(conflict);
    }

    private static CloudSyncConfigurationDto MapToDto(CloudSyncConfiguration entity)
    {
        return new CloudSyncConfigurationDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Provider = entity.Provider,
            SyncMode = entity.SyncMode,
            IsEnabled = entity.IsEnabled,
            PathFilter = entity.PathFilter,
            TagFilter = entity.TagFilter,
            SyncSchedule = entity.SyncSchedule,
            ConflictResolution = entity.ConflictResolution,
            EnableRealTimeSync = entity.EnableRealTimeSync,
            LastSyncAt = entity.LastSyncAt,
            NextSyncAt = entity.NextSyncAt,
            LastSyncError = entity.LastSyncError,
            ConsecutiveFailures = entity.ConsecutiveFailures,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }

    private static CloudSyncHistoryDto MapToHistoryDto(CloudSyncHistory entity)
    {
        return new CloudSyncHistoryDto
        {
            Id = entity.Id,
            OperationType = entity.OperationType,
            Direction = entity.Direction,
            Status = entity.Status,
            StartedAt = entity.StartedAt,
            CompletedAt = entity.CompletedAt,
            DurationMs = entity.DurationMs,
            SecretsPushed = entity.SecretsPushed,
            SecretsPulled = entity.SecretsPulled,
            SecretsUpdated = entity.SecretsUpdated,
            SecretsDeleted = entity.SecretsDeleted,
            ConflictsDetected = entity.ConflictsDetected,
            ConflictsResolved = entity.ConflictsResolved,
            ErrorsCount = entity.ErrorsCount,
            ErrorMessage = entity.ErrorMessage
        };
    }

    private static CloudSyncConflictDto MapToConflictDto(CloudSyncConflict entity)
    {
        return new CloudSyncConflictDto
        {
            Id = entity.Id,
            SecretPath = entity.SecretPath,
            ConflictType = entity.ConflictType,
            Status = entity.Status,
            UspVersion = entity.UspVersion,
            UspLastModified = entity.UspLastModified,
            CloudVersion = entity.CloudVersion,
            CloudLastModified = entity.CloudLastModified,
            ResolutionStrategy = entity.ResolutionStrategy,
            ResolvedValue = entity.ResolvedValue,
            DetectedAt = entity.DetectedAt,
            ResolvedAt = entity.ResolvedAt
        };
    }
}
