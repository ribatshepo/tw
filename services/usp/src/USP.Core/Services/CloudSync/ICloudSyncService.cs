using USP.Core.Models.DTOs.CloudSync;

namespace USP.Core.Services.CloudSync;

/// <summary>
/// Service for managing cloud secret synchronization
/// </summary>
public interface ICloudSyncService
{
    /// <summary>
    /// Configure cloud sync
    /// </summary>
    Task<CloudSyncConfigurationDto> ConfigureAsync(CreateCloudSyncConfigurationRequest request, Guid createdBy);

    /// <summary>
    /// Get cloud sync configuration by ID
    /// </summary>
    Task<CloudSyncConfigurationDto?> GetConfigurationAsync(Guid id);

    /// <summary>
    /// List all cloud sync configurations
    /// </summary>
    Task<List<CloudSyncConfigurationDto>> ListConfigurationsAsync();

    /// <summary>
    /// Update cloud sync configuration
    /// </summary>
    Task<CloudSyncConfigurationDto> UpdateConfigurationAsync(Guid id, UpdateCloudSyncConfigurationRequest request, Guid updatedBy);

    /// <summary>
    /// Delete cloud sync configuration
    /// </summary>
    Task DeleteConfigurationAsync(Guid id, Guid deletedBy);

    /// <summary>
    /// Trigger manual sync
    /// </summary>
    Task<CloudSyncHistoryDto> TriggerSyncAsync(Guid configurationId, TriggerSyncRequest request, Guid triggeredBy);

    /// <summary>
    /// Get sync status for a configuration
    /// </summary>
    Task<CloudSyncHistoryDto?> GetSyncStatusAsync(Guid configurationId);

    /// <summary>
    /// Get sync history
    /// </summary>
    Task<List<CloudSyncHistoryDto>> GetSyncHistoryAsync(Guid configurationId, int limit = 50);

    /// <summary>
    /// List conflicts for a configuration
    /// </summary>
    Task<List<CloudSyncConflictDto>> ListConflictsAsync(Guid configurationId, string? status = null);

    /// <summary>
    /// Get conflict by ID
    /// </summary>
    Task<CloudSyncConflictDto?> GetConflictAsync(Guid conflictId);

    /// <summary>
    /// Resolve conflict
    /// </summary>
    Task<CloudSyncConflictDto> ResolveConflictAsync(Guid conflictId, ResolveConflictRequest request, Guid resolvedBy);
}
